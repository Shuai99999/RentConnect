using Microsoft.EntityFrameworkCore;
using RentConnect.Application.Billing;
using RentConnect.Core.Entities;
using RentConnect.Core.Policies;
using RentConnect.Core.ValueObjects;
using RentConnect.Data;

namespace RentConnect.Infrastructure.Billing;

public sealed class SharedBillService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TimeProvider timeProvider)
    : ISharedBillService
{
    public async Task<IReadOnlyList<SharedBillTenancyOption>>
        GetAvailableTenanciesAsync(
            Guid userProfileId,
            CancellationToken cancellationToken = default)
    {
        EnsureId(userProfileId, nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);

        var today = GetToday();

        return await (
            from tenancy in context.Tenancies.AsNoTracking()
            join unit in context.RentalUnits.AsNoTracking()
                on tenancy.RentalUnitId equals unit.Id
            join property in context.Properties.AsNoTracking()
                on unit.PropertyId equals property.Id
            where !tenancy.IsCancelled &&
                  tenancy.StartDate <= today &&
                  (!tenancy.EndDate.HasValue ||
                   tenancy.EndDate.Value >= today) &&
                  (
                      context.TenancyMembers.Any(member =>
                          member.TenancyId == tenancy.Id &&
                          member.UserProfileId == userProfileId &&
                          member.JoinedOn <= today &&
                          (!member.LeftOn.HasValue ||
                           member.LeftOn.Value >= today))
                      ||
                      context.PropertyOwners.Any(owner =>
                          owner.PropertyId == property.Id &&
                          owner.UserProfileId == userProfileId)
                  )
            orderby property.Name, unit.Name
            select new SharedBillTenancyOption(
                tenancy.Id,
                property.Name,
                unit.Name))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SharedBillSummary>>
        GetVisibleBillsAsync(
            Guid userProfileId,
            CancellationToken cancellationToken = default)
    {
        EnsureId(userProfileId, nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);

        var today = GetToday();
        var activeTenancyIds = GetActiveTenancyIds(
            context,
            userProfileId,
            today);
        var ownedTenancyIds = GetOwnedTenancyIds(
            context,
            userProfileId);

        return await context.SharedBills
            .AsNoTracking()
            .Where(bill =>
                activeTenancyIds.Contains(bill.TenancyId) ||
                ownedTenancyIds.Contains(bill.TenancyId))
            .OrderByDescending(bill => bill.UpdatedAtUtc)
            .Select(bill => new SharedBillSummary(
                bill.Id,
                bill.TenancyId,
                bill.Title,
                bill.Category,
                bill.Total.Amount,
                bill.Total.Currency,
                bill.DueDate,
                bill.Status,
                bill.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<SharedBillDetails?> GetDetailsAsync(
        Guid sharedBillId,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        EnsureId(sharedBillId, nameof(sharedBillId));
        EnsureId(userProfileId, nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);

        var bill = await context.SharedBills
            .AsNoTracking()
            .Include(item => item.Shares)
            .Include(item => item.Disputes)
            .SingleOrDefaultAsync(
                item => item.Id == sharedBillId,
                cancellationToken);

        if (bill is null)
        {
            return null;
        }

        var access = await CreateAccessContextAsync(
            context,
            bill,
            userProfileId,
            cancellationToken);

        if (!SharedBillAccessPolicy.CanView(access))
        {
            return null;
        }

        var profileIds = bill.Shares
            .Select(share => share.UserProfileId)
            .Concat(bill.Disputes.Select(
                dispute => dispute.RaisedByUserProfileId))
            .Concat(bill.Disputes
                .Where(dispute =>
                    dispute.ResolvedByUserProfileId.HasValue)
                .Select(dispute =>
                    dispute.ResolvedByUserProfileId!.Value))
            .Distinct()
            .ToArray();

        var displayNames = await context.UserProfiles
            .AsNoTracking()
            .Where(profile => profileIds.Contains(profile.Id))
            .ToDictionaryAsync(
                profile => profile.Id,
                profile => profile.DisplayName,
                cancellationToken);

        var shares = bill.Shares
            .OrderBy(share =>
                displayNames[share.UserProfileId])
            .Select(share => new BillShareItem(
                share.UserProfileId,
                displayNames[share.UserProfileId],
                share.Amount.Amount,
                share.Amount.Currency,
                share.PaymentDeclaredAtUtc,
                share.PaymentConfirmedAtUtc))
            .ToArray();

        var disputes = bill.Disputes
            .OrderBy(dispute => dispute.RaisedAtUtc)
            .Select(dispute => new BillDisputeItem(
                dispute.Id,
                dispute.RaisedByUserProfileId,
                displayNames[dispute.RaisedByUserProfileId],
                dispute.Reason,
                dispute.RaisedAtUtc,
                dispute.ResolvedByUserProfileId,
                dispute.ResolvedByUserProfileId.HasValue
                    ? displayNames[
                        dispute.ResolvedByUserProfileId.Value]
                    : null,
                dispute.Resolution,
                dispute.ResolvedAtUtc))
            .ToArray();

        return new SharedBillDetails(
            bill.Id,
            bill.TenancyId,
            bill.CreatedByUserProfileId,
            bill.PaidByUserProfileId,
            bill.Title,
            bill.Category,
            bill.Total.Amount,
            bill.Total.Currency,
            bill.PeriodStart,
            bill.PeriodEnd,
            bill.DueDate,
            bill.SplitMethod,
            bill.Status,
            bill.CreatedAtUtc,
            bill.UpdatedAtUtc,
            shares,
            disputes,
            CreatePermissions(bill, access));
    }

    public async Task<Guid> CreateEqualSplitAsync(
        CreateEqualSharedBillCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(userProfileId, nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);

        var tenancy = await context.Tenancies
            .Include(item => item.Members)
            .SingleOrDefaultAsync(
                item => item.Id == command.TenancyId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "The tenancy was not found.");

        var today = GetToday();
        var activeMemberIds = tenancy.Members
            .Where(member => member.IsActiveOn(today))
            .Select(member => member.UserProfileId)
            .Distinct()
            .ToArray();

        var ownerIds = await (
                from unit in context.RentalUnits.AsNoTracking()
                join owner in context.PropertyOwners.AsNoTracking()
                    on unit.PropertyId equals owner.PropertyId
                where unit.Id == tenancy.RentalUnitId
                select owner.UserProfileId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var access = new SharedBillAccessContext(
            userProfileId,
            ownerIds.Contains(userProfileId),
            tenancy.IsMemberActiveOn(userProfileId, today),
            activeMemberIds.Contains(userProfileId));

        if (!tenancy.IsActiveOn(today) ||
            !SharedBillAccessPolicy.CanCreate(access))
        {
            throw new KeyNotFoundException(
                "The tenancy was not found.");
        }

        if (activeMemberIds.Length == 0)
        {
            throw new InvalidOperationException(
                "The tenancy has no active members to share this bill.");
        }

        if (!activeMemberIds.Contains(command.PaidByUserProfileId) &&
            !ownerIds.Contains(command.PaidByUserProfileId))
        {
            throw new ArgumentException(
                "The paying user must be an active member or property owner.",
                nameof(command));
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var bill = new SharedBill(
            Guid.NewGuid(),
            tenancy.Id,
            userProfileId,
            command.PaidByUserProfileId,
            command.Title,
            command.Category,
            new Money(command.TotalAmount, command.Currency),
            command.PeriodStart,
            command.PeriodEnd,
            command.DueDate,
            now);

        bill.AllocateEqually(
            activeMemberIds.Select(memberId =>
                (Guid.NewGuid(), memberId)),
            command.CurrencyDecimalPlaces,
            now);

        context.SharedBills.Add(bill);
        await context.SaveChangesAsync(cancellationToken);

        return bill.Id;
    }

    public async Task PublishAsync(
        PublishSharedBillCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(userProfileId, nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        var bill = await GetBillAsync(
            context,
            command.SharedBillId,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            context,
            bill,
            userProfileId,
            cancellationToken);

        Demand(SharedBillAccessPolicy.CanPublish(bill, access));

        bill.Publish(GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task VoidAsync(
        VoidSharedBillCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(userProfileId, nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        var bill = await GetBillAsync(
            context,
            command.SharedBillId,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            context,
            bill,
            userProfileId,
            cancellationToken);

        Demand(SharedBillAccessPolicy.CanVoid(bill, access));

        bill.Void(GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeclarePaymentAsync(
        DeclareBillPaymentCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(userProfileId, nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        var bill = await GetBillAsync(
            context,
            command.SharedBillId,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            context,
            bill,
            userProfileId,
            cancellationToken);

        Demand(
            SharedBillAccessPolicy.CanDeclarePayment(bill, access));

        bill.DeclareSharePayment(userProfileId, GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ConfirmPaymentAsync(
        ConfirmBillPaymentCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(userProfileId, nameof(userProfileId));
        EnsureId(command.UserProfileId, nameof(command.UserProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        var bill = await GetBillAsync(
            context,
            command.SharedBillId,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            context,
            bill,
            userProfileId,
            cancellationToken);

        Demand(
            SharedBillAccessPolicy.CanConfirmPayment(bill, access));

        bill.ConfirmSharePayment(
            command.UserProfileId,
            GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RaiseDisputeAsync(
        RaiseBillDisputeCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(userProfileId, nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        var bill = await GetBillAsync(
            context,
            command.SharedBillId,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            context,
            bill,
            userProfileId,
            cancellationToken);

        Demand(SharedBillAccessPolicy.CanRaiseDispute(bill, access));

        var disputeId = Guid.NewGuid();
        bill.RaiseDispute(
            disputeId,
            userProfileId,
            command.Reason,
            GetUtcNow());

        context.BillDisputes.Add(
            bill.Disputes.Single(dispute => dispute.Id == disputeId));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ResolveDisputeAsync(
        ResolveBillDisputeCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(userProfileId, nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        var bill = await GetBillAsync(
            context,
            command.SharedBillId,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            context,
            bill,
            userProfileId,
            cancellationToken);

        Demand(SharedBillAccessPolicy.CanResolveDispute(bill, access));

        bill.ResolveDispute(
            command.DisputeId,
            userProfileId,
            command.Resolution,
            GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<SharedBill> GetBillAsync(
        ApplicationDbContext context,
        Guid sharedBillId,
        CancellationToken cancellationToken)
    {
        EnsureId(sharedBillId, nameof(sharedBillId));

        return await context.SharedBills
                   .Include(bill => bill.Shares)
                   .Include(bill => bill.Disputes)
                   .SingleOrDefaultAsync(
                       bill => bill.Id == sharedBillId,
                       cancellationToken)
               ?? throw new KeyNotFoundException(
                   "The shared bill was not found.");
    }

    private async Task<SharedBillAccessContext>
        CreateAccessContextAsync(
            ApplicationDbContext context,
            SharedBill bill,
            Guid userProfileId,
            CancellationToken cancellationToken)
    {
        var today = GetToday();

        var isActiveMember = await GetActiveTenancyIds(
                context,
                userProfileId,
                today)
            .AnyAsync(
                tenancyId => tenancyId == bill.TenancyId,
                cancellationToken);

        var isOwner = await GetOwnedTenancyIds(
                context,
                userProfileId)
            .AnyAsync(
                tenancyId => tenancyId == bill.TenancyId,
                cancellationToken);

        return new SharedBillAccessContext(
            userProfileId,
            isOwner,
            isActiveMember,
            bill.Shares.Any(
                share => share.UserProfileId == userProfileId));
    }

    private static IQueryable<Guid> GetActiveTenancyIds(
        ApplicationDbContext context,
        Guid userProfileId,
        DateOnly today)
    {
        return
            from tenancy in context.Tenancies
            join member in context.TenancyMembers
                on tenancy.Id equals member.TenancyId
            where member.UserProfileId == userProfileId &&
                  !tenancy.IsCancelled &&
                  tenancy.StartDate <= today &&
                  (!tenancy.EndDate.HasValue ||
                   tenancy.EndDate.Value >= today) &&
                  member.JoinedOn <= today &&
                  (!member.LeftOn.HasValue ||
                   member.LeftOn.Value >= today)
            select tenancy.Id;
    }

    private static IQueryable<Guid> GetOwnedTenancyIds(
        ApplicationDbContext context,
        Guid userProfileId)
    {
        return
            from tenancy in context.Tenancies
            join unit in context.RentalUnits
                on tenancy.RentalUnitId equals unit.Id
            join owner in context.PropertyOwners
                on unit.PropertyId equals owner.PropertyId
            where owner.UserProfileId == userProfileId
            select tenancy.Id;
    }

    private static SharedBillPermissions CreatePermissions(
        SharedBill bill,
        SharedBillAccessContext access)
    {
        return new SharedBillPermissions(
            SharedBillAccessPolicy.CanPublish(bill, access),
            SharedBillAccessPolicy.CanVoid(bill, access),
            SharedBillAccessPolicy.CanDeclarePayment(bill, access),
            SharedBillAccessPolicy.CanConfirmPayment(bill, access),
            SharedBillAccessPolicy.CanRaiseDispute(bill, access),
            SharedBillAccessPolicy.CanResolveDispute(bill, access));
    }

    private DateOnly GetToday()
    {
        return DateOnly.FromDateTime(
            timeProvider.GetUtcNow().UtcDateTime);
    }

    private DateTimeOffset GetUtcNow()
    {
        return timeProvider.GetUtcNow().ToUniversalTime();
    }

    private static void Demand(bool isAllowed)
    {
        if (!isAllowed)
        {
            throw new KeyNotFoundException(
                "The shared bill was not found.");
        }
    }

    private static void EnsureId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "ID cannot be empty.",
                parameterName);
        }
    }
}
