using Microsoft.EntityFrameworkCore;
using RentConnect.Application.Properties;
using RentConnect.Core.Entities;
using RentConnect.Core.ValueObjects;
using RentConnect.Data;

namespace RentConnect.Infrastructure.Properties;

public sealed class PropertyService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TimeProvider timeProvider)
    : IPropertyService
{
    public async Task<IReadOnlyList<PropertySummary>>
        GetVisiblePropertiesAsync(
            Guid userProfileId,
            CancellationToken cancellationToken = default)
    {
        EnsureId(userProfileId, nameof(userProfileId));
        var today = GetToday();

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);

        var ownedPropertyIds = context.PropertyOwners
            .Where(owner => owner.UserProfileId == userProfileId)
            .Select(owner => owner.PropertyId);

        var activeTenantUnitIds = GetActiveTenantUnitIds(
            context,
            userProfileId,
            today);

        var tenantPropertyIds = context.RentalUnits
            .Where(unit => activeTenantUnitIds.Contains(unit.Id))
            .Select(unit => unit.PropertyId);

        var visiblePropertyIds = ownedPropertyIds
            .Concat(tenantPropertyIds)
            .Distinct();

        return await context.Properties
            .AsNoTracking()
            .Where(property => visiblePropertyIds.Contains(property.Id))
            .OrderBy(property => property.Name)
            .Select(property => new PropertySummary(
                property.Id,
                property.Name,
                property.Address.Line1,
                property.Address.City,
                property.Address.Region,
                property.Address.CountryCode,
                context.RentalUnits.Count(unit =>
                    unit.PropertyId == property.Id &&
                    (ownedPropertyIds.Contains(property.Id) ||
                     activeTenantUnitIds.Contains(unit.Id))),
                context.Tenancies.Count(tenancy =>
                    context.RentalUnits.Any(unit =>
                        unit.Id == tenancy.RentalUnitId &&
                        unit.PropertyId == property.Id &&
                        (ownedPropertyIds.Contains(property.Id) ||
                         activeTenantUnitIds.Contains(unit.Id))) &&
                    !tenancy.IsCancelled &&
                    tenancy.StartDate <= today &&
                    (!tenancy.EndDate.HasValue ||
                     tenancy.EndDate.Value >= today)),
                ownedPropertyIds.Contains(property.Id)))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<PropertyDetails?> GetDetailsAsync(
        Guid propertyId,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        EnsureId(propertyId, nameof(propertyId));
        EnsureId(userProfileId, nameof(userProfileId));
        var today = GetToday();

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);

        var property = await context.Properties
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == propertyId,
                cancellationToken);

        if (property is null)
        {
            return null;
        }

        var canManage = await IsOwnerAsync(
            context,
            propertyId,
            userProfileId,
            cancellationToken);

        var activeTenantUnitIds = await GetActiveTenantUnitIds(
                context,
                userProfileId,
                today)
            .ToArrayAsync(cancellationToken);

        if (!canManage && !await context.RentalUnits.AnyAsync(
                unit => unit.PropertyId == propertyId &&
                        activeTenantUnitIds.Contains(unit.Id),
                cancellationToken))
        {
            return null;
        }

        var visibleUnits = await context.RentalUnits
            .AsNoTracking()
            .Where(unit =>
                unit.PropertyId == propertyId &&
                (canManage || activeTenantUnitIds.Contains(unit.Id)))
            .OrderBy(unit => unit.Name)
            .Select(unit => new
            {
                Unit = unit,
                Tenancy = context.Tenancies
                    .Where(tenancy =>
                        tenancy.RentalUnitId == unit.Id &&
                        !tenancy.IsCancelled &&
                        tenancy.StartDate <= today &&
                        (!tenancy.EndDate.HasValue ||
                         tenancy.EndDate.Value >= today))
                    .OrderByDescending(tenancy => tenancy.StartDate)
                    .Select(tenancy => new
                    {
                        tenancy.Id,
                        tenancy.StartDate,
                        tenancy.EndDate
                    })
                    .FirstOrDefault(),
                Listing = context.RentalListings
                    .Where(listing => listing.RentalUnitId == unit.Id)
                    .Select(listing => new
                    {
                        listing.Id,
                        listing.IsPublished
                    })
                    .SingleOrDefault()
            })
            .ToArrayAsync(cancellationToken);

        var tenancyIds = visibleUnits
            .Where(item => item.Tenancy is not null)
            .Select(item => item.Tenancy!.Id)
            .ToArray();

        var tenantCounts = await context.TenancyMembers
            .AsNoTracking()
            .Where(member =>
                tenancyIds.Contains(member.TenancyId) &&
                member.JoinedOn <= today &&
                (!member.LeftOn.HasValue ||
                 member.LeftOn.Value >= today))
            .GroupBy(member => member.TenancyId)
            .ToDictionaryAsync(
                group => group.Key,
                group => group.Count(),
                cancellationToken);

        var ownerNames = await (
                from owner in context.PropertyOwners.AsNoTracking()
                join profile in context.UserProfiles.AsNoTracking()
                    on owner.UserProfileId equals profile.Id
                where owner.PropertyId == propertyId
                orderby profile.DisplayName
                select profile.DisplayName)
            .ToArrayAsync(cancellationToken);

        var units = visibleUnits.Select(item =>
        {
            var tenancy = item.Tenancy;
            var tenantCount = tenancy is null
                ? 0
                : tenantCounts.GetValueOrDefault(tenancy.Id);

            return new RentalUnitItem(
                item.Unit.Id,
                item.Unit.Name,
                item.Unit.Type,
                item.Unit.IsActive,
                tenancy is not null,
                tenancy?.Id,
                tenantCount,
                tenancy?.StartDate,
                tenancy?.EndDate,
                item.Listing == null ? null : item.Listing.Id,
                item.Listing != null && item.Listing.IsPublished);
        }).ToArray();

        return new PropertyDetails(
            property.Id,
            property.Name,
            property.Address.Line1,
            property.Address.Line2,
            property.Address.City,
            property.Address.Region,
            property.Address.PostalCode,
            property.Address.CountryCode,
            canManage,
            ownerNames,
            units);
    }

    public async Task<Guid> CreateAsync(
        CreatePropertyCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(userProfileId, nameof(userProfileId));

        var propertyId = Guid.NewGuid();
        var property = new Property(
            propertyId,
            command.Name,
            CreateAddress(command),
            userProfileId);

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Properties.Add(property);
        await context.SaveChangesAsync(cancellationToken);
        return propertyId;
    }

    public async Task UpdateAsync(
        UpdatePropertyCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(command.PropertyId, nameof(command.PropertyId));
        EnsureId(userProfileId, nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        await DemandOwnerAsync(
            context,
            command.PropertyId,
            userProfileId,
            cancellationToken);

        var property = await context.Properties.SingleAsync(
            item => item.Id == command.PropertyId,
            cancellationToken);
        property.Rename(command.Name);
        property.ChangeAddress(CreateAddress(command));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> AddRentalUnitAsync(
        AddRentalUnitCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(command.PropertyId, nameof(command.PropertyId));
        EnsureId(userProfileId, nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        await DemandOwnerAsync(
            context,
            command.PropertyId,
            userProfileId,
            cancellationToken);

        var unit = new RentalUnit(
            Guid.NewGuid(),
            command.PropertyId,
            command.Name,
            command.Type);
        context.RentalUnits.Add(unit);
        await context.SaveChangesAsync(cancellationToken);
        return unit.Id;
    }

    public async Task UpdateRentalUnitAsync(
        UpdateRentalUnitCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(command.RentalUnitId, nameof(command.RentalUnitId));
        EnsureId(userProfileId, nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        var unit = await GetManagedUnitAsync(
            context,
            command.RentalUnitId,
            userProfileId,
            cancellationToken);
        unit.Rename(command.Name);

        if (unit.Type != command.Type)
        {
            unit.ChangeType(command.Type);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetRentalUnitActiveAsync(
        SetRentalUnitActiveCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(command.RentalUnitId, nameof(command.RentalUnitId));
        EnsureId(userProfileId, nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        var unit = await GetManagedUnitAsync(
            context,
            command.RentalUnitId,
            userProfileId,
            cancellationToken);

        if (command.IsActive)
        {
            unit.Activate();
        }
        else
        {
            var hasActiveTenancy = await context.Tenancies.AnyAsync(
                tenancy =>
                    tenancy.RentalUnitId == unit.Id &&
                    !tenancy.IsCancelled &&
                    tenancy.StartDate <= GetToday() &&
                    (!tenancy.EndDate.HasValue ||
                     tenancy.EndDate.Value >= GetToday()),
                cancellationToken);

            if (hasActiveTenancy)
            {
                throw new InvalidOperationException(
                    "A rental unit with an active tenancy cannot be deactivated.");
            }

            unit.Deactivate();
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Address CreateAddress(CreatePropertyCommand command) =>
        new(
            command.AddressLine1,
            command.AddressLine2,
            command.City,
            command.Region,
            command.PostalCode,
            command.CountryCode);

    private static Address CreateAddress(UpdatePropertyCommand command) =>
        new(
            command.AddressLine1,
            command.AddressLine2,
            command.City,
            command.Region,
            command.PostalCode,
            command.CountryCode);

    private static IQueryable<Guid> GetActiveTenantUnitIds(
        ApplicationDbContext context,
        Guid userProfileId,
        DateOnly today) =>
        from tenancy in context.Tenancies
        join member in context.TenancyMembers
            on tenancy.Id equals member.TenancyId
        where member.UserProfileId == userProfileId &&
              !tenancy.IsCancelled &&
              tenancy.StartDate <= today &&
              (!tenancy.EndDate.HasValue || tenancy.EndDate.Value >= today) &&
              member.JoinedOn <= today &&
              (!member.LeftOn.HasValue || member.LeftOn.Value >= today)
        select tenancy.RentalUnitId;

    private static Task<bool> IsOwnerAsync(
        ApplicationDbContext context,
        Guid propertyId,
        Guid userProfileId,
        CancellationToken cancellationToken) =>
        context.PropertyOwners.AnyAsync(
            owner => owner.PropertyId == propertyId &&
                     owner.UserProfileId == userProfileId,
            cancellationToken);

    private static async Task DemandOwnerAsync(
        ApplicationDbContext context,
        Guid propertyId,
        Guid userProfileId,
        CancellationToken cancellationToken)
    {
        if (!await IsOwnerAsync(
                context,
                propertyId,
                userProfileId,
                cancellationToken))
        {
            throw new KeyNotFoundException("The property was not found.");
        }
    }

    private static async Task<RentalUnit> GetManagedUnitAsync(
        ApplicationDbContext context,
        Guid rentalUnitId,
        Guid userProfileId,
        CancellationToken cancellationToken)
    {
        var unit = await context.RentalUnits.SingleOrDefaultAsync(
            item => item.Id == rentalUnitId,
            cancellationToken);

        if (unit is null || !await IsOwnerAsync(
                context,
                unit.PropertyId,
                userProfileId,
                cancellationToken))
        {
            throw new KeyNotFoundException("The rental unit was not found.");
        }

        return unit;
    }

    private DateOnly GetToday() =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private static void EnsureId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The ID cannot be empty.", parameterName);
        }
    }
}
