using Microsoft.EntityFrameworkCore;
using RentConnect.Application.Listings;
using RentConnect.Core.Entities;
using RentConnect.Core.Enums;
using RentConnect.Data;

namespace RentConnect.Infrastructure.Listings;

public sealed class RentalListingService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TimeProvider timeProvider) : IRentalListingService
{
    public async Task<IReadOnlyList<PublicListingSummary>> GetPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await (
                from listing in context.RentalListings.AsNoTracking()
                join unit in context.RentalUnits.AsNoTracking() on listing.RentalUnitId equals unit.Id
                join property in context.Properties.AsNoTracking() on unit.PropertyId equals property.Id
                where listing.IsPublished && unit.IsActive
                orderby listing.CreatedAtUtc descending
                select new PublicListingSummary(
                    listing.Id, listing.Title, property.Name, unit.Name, unit.Type,
                    property.Address.City, property.Address.Region, listing.MonthlyRent,
                    listing.AvailableFrom))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<PublicListingDetails?> GetDetailsAsync(
        Guid listingId, Guid userProfileId, CancellationToken cancellationToken = default)
    {
        EnsureId(listingId, nameof(listingId));
        EnsureId(userProfileId, nameof(userProfileId));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var details = await (
                from listing in context.RentalListings.AsNoTracking()
                join unit in context.RentalUnits.AsNoTracking() on listing.RentalUnitId equals unit.Id
                join property in context.Properties.AsNoTracking() on unit.PropertyId equals property.Id
                where listing.Id == listingId
                let canManage = context.PropertyOwners.Any(owner =>
                    owner.PropertyId == property.Id && owner.UserProfileId == userProfileId)
                where listing.IsPublished || canManage
                select new
                {
                    Listing = listing,
                    Unit = unit,
                    Property = property,
                    CanManage = canManage
                })
            .SingleOrDefaultAsync(cancellationToken);

        if (details is null) return null;

        var applicationStatus = await context.RentalApplications.AsNoTracking()
            .Where(application => application.RentalListingId == listingId &&
                                  application.ApplicantUserProfileId == userProfileId)
            .Select(application => (RentalApplicationStatus?)application.Status)
            .SingleOrDefaultAsync(cancellationToken);

        return new PublicListingDetails(
            details.Listing.Id, details.Unit.Id, details.Listing.Title, details.Listing.Description,
            details.Property.Name, details.Unit.Name, details.Unit.Type,
            details.Property.Address.City, details.Property.Address.Region,
            details.Property.Address.CountryCode, details.Listing.MonthlyRent,
            details.Listing.AvailableFrom, details.CanManage, applicationStatus);
    }

    public async Task<IReadOnlyList<OwnedListingUnit>> GetOwnedUnitsAsync(
        Guid userProfileId, CancellationToken cancellationToken = default)
    {
        EnsureId(userProfileId, nameof(userProfileId));
        var today = Today;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await (
                from unit in context.RentalUnits.AsNoTracking()
                join property in context.Properties.AsNoTracking() on unit.PropertyId equals property.Id
                where context.PropertyOwners.Any(owner => owner.PropertyId == property.Id &&
                                                           owner.UserProfileId == userProfileId)
                let listing = context.RentalListings.FirstOrDefault(item => item.RentalUnitId == unit.Id)
                orderby property.Name, unit.Name
                select new OwnedListingUnit(
                    unit.Id, property.Name, unit.Name, unit.Type, unit.IsActive,
                    context.Tenancies.Any(tenancy => tenancy.RentalUnitId == unit.Id &&
                        !tenancy.IsCancelled && (!tenancy.EndDate.HasValue || tenancy.EndDate.Value >= today) &&
                        context.TenancyMembers.Any(member => member.TenancyId == tenancy.Id)),
                    listing == null ? null : listing.Id,
                    listing == null ? null : listing.Title,
                    listing == null ? null : listing.Description,
                    listing == null ? null : listing.MonthlyRent,
                    listing == null ? null : listing.AvailableFrom,
                    listing != null && listing.IsPublished))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<OwnedListingUnit?> GetOwnedUnitAsync(
        Guid rentalUnitId, Guid userProfileId, CancellationToken cancellationToken = default)
    {
        EnsureId(rentalUnitId, nameof(rentalUnitId));
        var units = await GetOwnedUnitsAsync(userProfileId, cancellationToken);
        return units.SingleOrDefault(unit => unit.RentalUnitId == rentalUnitId);
    }

    public Task<Guid> PublishAsync(
        PublishListingCommand command, Guid userProfileId, CancellationToken cancellationToken = default)
        => SaveAsync(command, true, userProfileId, cancellationToken);

    public async Task<Guid> SaveAsync(
        PublishListingCommand command, bool publish, Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(command.RentalUnitId, nameof(command.RentalUnitId));
        EnsureId(userProfileId, nameof(userProfileId));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var unit = await GetManagedUnitAsync(context, command.RentalUnitId, userProfileId, cancellationToken);
        if (publish)
        {
            if (!unit.IsActive) throw new InvalidOperationException("Activate the rental space before publishing it.");
            await EnsureNoOccupiedTenancyAsync(context, unit.Id, command.AvailableFrom, cancellationToken);
        }

        var listing = await context.RentalListings.SingleOrDefaultAsync(
            item => item.RentalUnitId == unit.Id, cancellationToken);
        if (listing is null)
        {
            listing = new RentalListing(
                Guid.NewGuid(), unit.Id, command.Title, command.Description,
                command.MonthlyRent, command.AvailableFrom, timeProvider.GetUtcNow());
            if (!publish) listing.Unpublish();
            context.RentalListings.Add(listing);
        }
        else
        {
            listing.Update(command.Title, command.Description, command.MonthlyRent, command.AvailableFrom);
            if (publish) listing.Publish();
            else listing.Unpublish();
        }

        await context.SaveChangesAsync(cancellationToken);
        return listing.Id;
    }

    public async Task SetPublishedAsync(
        Guid listingId, bool isPublished, Guid userProfileId, CancellationToken cancellationToken = default)
    {
        var listing = await GetManagedListingAsync(listingId, userProfileId, cancellationToken);
        await using var context = listing.Context;
        if (isPublished)
        {
            var unit = await context.RentalUnits.SingleAsync(item => item.Id == listing.Listing.RentalUnitId, cancellationToken);
            if (!unit.IsActive) throw new InvalidOperationException("Activate the rental space before publishing it.");
            await EnsureNoOccupiedTenancyAsync(context, unit.Id, listing.Listing.AvailableFrom, cancellationToken);
            listing.Listing.Publish();
        }
        else
        {
            listing.Listing.Unpublish();
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyAsync(
        ApplyForListingCommand command, Guid userProfileId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(command.ListingId, nameof(command.ListingId));
        EnsureId(userProfileId, nameof(userProfileId));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var listingInfo = await (
                from listing in context.RentalListings
                join unit in context.RentalUnits on listing.RentalUnitId equals unit.Id
                join property in context.Properties on unit.PropertyId equals property.Id
                where listing.Id == command.ListingId && listing.IsPublished && unit.IsActive
                select new { Listing = listing, Property = property, Unit = unit })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("The listing is not available.");

        if (await context.PropertyOwners.AnyAsync(owner =>
                owner.PropertyId == listingInfo.Property.Id && owner.UserProfileId == userProfileId,
                cancellationToken))
            throw new InvalidOperationException("Property owners cannot apply to their own listing.");

        if (await context.RentalApplications.AnyAsync(application =>
                application.RentalListingId == command.ListingId &&
                application.ApplicantUserProfileId == userProfileId, cancellationToken))
            throw new InvalidOperationException("You have already applied for this listing.");

        var now = timeProvider.GetUtcNow();
        var application = new RentalApplication(
            Guid.NewGuid(), command.ListingId, userProfileId, command.Message, now);
        context.RentalApplications.Add(application);

        var applicantName = await context.UserProfiles
            .Where(profile => profile.Id == userProfileId)
            .Select(profile => profile.DisplayName)
            .SingleAsync(cancellationToken);
        var ownerIds = await context.PropertyOwners
            .Where(owner => owner.PropertyId == listingInfo.Property.Id)
            .Select(owner => owner.UserProfileId)
            .ToArrayAsync(cancellationToken);
        foreach (var ownerId in ownerIds)
        {
            context.UserNotifications.Add(new UserNotification(
                Guid.NewGuid(), ownerId, userProfileId,
                NotificationType.RentalApplicationSubmitted,
                "New rental application",
                $"{applicantName} applied for {listingInfo.Listing.Title}.",
                "/rental-applications", now));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ManagedRentalApplication>> GetManagedApplicationsAsync(
        Guid userProfileId, CancellationToken cancellationToken = default)
    {
        EnsureId(userProfileId, nameof(userProfileId));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await (
                from application in context.RentalApplications.AsNoTracking()
                join listing in context.RentalListings.AsNoTracking() on application.RentalListingId equals listing.Id
                join unit in context.RentalUnits.AsNoTracking() on listing.RentalUnitId equals unit.Id
                join property in context.Properties.AsNoTracking() on unit.PropertyId equals property.Id
                join profile in context.UserProfiles.AsNoTracking() on application.ApplicantUserProfileId equals profile.Id
                join user in context.Users.AsNoTracking() on profile.Id equals user.UserProfileId
                where context.PropertyOwners.Any(owner => owner.PropertyId == property.Id &&
                                                           owner.UserProfileId == userProfileId)
                orderby application.Status, application.CreatedAtUtc descending
                select new ManagedRentalApplication(
                    application.Id, listing.Id, listing.Title, property.Name, unit.Name,
                    profile.Id, profile.DisplayName, user.Email ?? string.Empty,
                    application.Message, application.Status, application.CreatedAtUtc,
                    application.ResolvedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Guid> ApproveAsync(
        Guid applicationId, Guid userProfileId, CancellationToken cancellationToken = default)
    {
        EnsureId(applicationId, nameof(applicationId));
        EnsureId(userProfileId, nameof(userProfileId));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var data = await GetManagedApplicationAsync(context, applicationId, userProfileId, cancellationToken);
        if (!data.Listing.IsPublished) throw new InvalidOperationException("The listing is no longer published.");
        if (data.Application.Status != RentalApplicationStatus.Pending) throw new InvalidOperationException("The application has already been resolved.");

        var startDate = data.Listing.AvailableFrom > Today ? data.Listing.AvailableFrom : Today;
        await EnsureNoOccupiedTenancyAsync(context, data.Listing.RentalUnitId, startDate, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var tenancy = await context.Tenancies
            .Include(item => item.Members)
            .Where(item => item.RentalUnitId == data.Listing.RentalUnitId &&
                           !item.IsCancelled &&
                           (!item.EndDate.HasValue || item.EndDate.Value >= startDate) &&
                           !item.Members.Any())
            .OrderBy(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
        var isNewTenancy = tenancy is null;
        tenancy ??= new Tenancy(Guid.NewGuid(), data.Listing.RentalUnitId, startDate, null);
        var joinedOn = tenancy.StartDate > startDate ? tenancy.StartDate : startDate;
        var membership = tenancy.AddMember(
            Guid.NewGuid(), data.Application.ApplicantUserProfileId, joinedOn);
        if (isNewTenancy)
            context.Tenancies.Add(tenancy);
        else
            context.TenancyMembers.Add(membership);
        data.Application.Approve(now);
        data.Listing.Unpublish();

        var otherPending = await context.RentalApplications
            .Where(item => item.RentalListingId == data.Listing.Id && item.Id != applicationId &&
                           item.Status == RentalApplicationStatus.Pending)
            .ToArrayAsync(cancellationToken);
        foreach (var other in otherPending)
        {
            other.Reject(now);
            AddDecisionNotification(context, other.ApplicantUserProfileId, userProfileId,
                data.Listing, false, now);
        }
        AddDecisionNotification(context, data.Application.ApplicantUserProfileId, userProfileId,
            data.Listing, true, now, tenancy.Id);

        await context.SaveChangesAsync(cancellationToken);
        return tenancy.Id;
    }

    public async Task RejectAsync(
        Guid applicationId, Guid userProfileId, CancellationToken cancellationToken = default)
    {
        EnsureId(applicationId, nameof(applicationId));
        EnsureId(userProfileId, nameof(userProfileId));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var data = await GetManagedApplicationAsync(context, applicationId, userProfileId, cancellationToken);
        data.Application.Reject(timeProvider.GetUtcNow());
        AddDecisionNotification(context, data.Application.ApplicantUserProfileId, userProfileId,
            data.Listing, false, timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<(ApplicationDbContext Context, RentalListing Listing)> GetManagedListingAsync(
        Guid listingId, Guid userProfileId, CancellationToken cancellationToken)
    {
        EnsureId(listingId, nameof(listingId));
        EnsureId(userProfileId, nameof(userProfileId));
        var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var listing = await (
                from item in context.RentalListings
                join unit in context.RentalUnits on item.RentalUnitId equals unit.Id
                where item.Id == listingId && context.PropertyOwners.Any(owner =>
                    owner.PropertyId == unit.PropertyId && owner.UserProfileId == userProfileId)
                select item)
            .SingleOrDefaultAsync(cancellationToken);
        if (listing is null)
        {
            await context.DisposeAsync();
            throw new KeyNotFoundException("The listing was not found.");
        }
        return (context, listing);
    }

    private static async Task<(RentalApplication Application, RentalListing Listing)> GetManagedApplicationAsync(
        ApplicationDbContext context, Guid applicationId, Guid userProfileId, CancellationToken cancellationToken)
    {
        var data = await (
                from application in context.RentalApplications
                join listing in context.RentalListings on application.RentalListingId equals listing.Id
                join unit in context.RentalUnits on listing.RentalUnitId equals unit.Id
                where application.Id == applicationId && context.PropertyOwners.Any(owner =>
                    owner.PropertyId == unit.PropertyId && owner.UserProfileId == userProfileId)
                select new { Application = application, Listing = listing })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("The rental application was not found.");
        return (data.Application, data.Listing);
    }

    private static async Task<RentalUnit> GetManagedUnitAsync(
        ApplicationDbContext context, Guid rentalUnitId, Guid userProfileId, CancellationToken cancellationToken)
    {
        return await (
                from unit in context.RentalUnits
                where unit.Id == rentalUnitId && context.PropertyOwners.Any(owner =>
                    owner.PropertyId == unit.PropertyId && owner.UserProfileId == userProfileId)
                select unit)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("The rental space was not found.");
    }

    private static async Task EnsureNoOccupiedTenancyAsync(
        ApplicationDbContext context, Guid rentalUnitId, DateOnly startDate, CancellationToken cancellationToken)
    {
        var overlaps = await context.Tenancies.AnyAsync(tenancy =>
            tenancy.RentalUnitId == rentalUnitId && !tenancy.IsCancelled &&
            (!tenancy.EndDate.HasValue || tenancy.EndDate.Value >= startDate) &&
            context.TenancyMembers.Any(member => member.TenancyId == tenancy.Id), cancellationToken);
        if (overlaps) throw new InvalidOperationException("This rental space already has a tenancy with members.");
    }

    private static void AddDecisionNotification(
        ApplicationDbContext context, Guid recipientId, Guid actorId,
        RentalListing listing, bool approved, DateTimeOffset now, Guid? tenancyId = null)
    {
        context.UserNotifications.Add(new UserNotification(
            Guid.NewGuid(), recipientId, actorId,
            approved ? NotificationType.RentalApplicationApproved : NotificationType.RentalApplicationRejected,
            approved ? "Rental application approved" : "Rental application update",
            approved ? $"Your application for {listing.Title} was approved." : $"Your application for {listing.Title} was not selected.",
            approved && tenancyId.HasValue ? $"/tenancies/{tenancyId}" : $"/listings/{listing.Id}", now));
    }

    private DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private static void EnsureId(Guid id, string parameterName)
    {
        if (id == Guid.Empty) throw new ArgumentException("ID cannot be empty.", parameterName);
    }
}
