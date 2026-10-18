using Microsoft.EntityFrameworkCore;
using RentConnect.Application.Billing;
using RentConnect.Core.Entities;
using RentConnect.Core.Enums;
using RentConnect.Core.Policies;
using RentConnect.Core.ValueObjects;
using RentConnect.Data;

namespace RentConnect.Infrastructure.Billing;

public sealed class SharedBillService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TimeProvider timeProvider,
    IBillAttachmentStorage attachmentStorage)
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

    public async Task<IReadOnlyList<SharedBillParticipantOption>>
        GetTenancyParticipantsAsync(
            Guid tenancyId,
            Guid userProfileId,
            CancellationToken cancellationToken = default)
    {
        EnsureId(tenancyId, nameof(tenancyId));
        EnsureId(userProfileId, nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        var today = GetToday();
        var canAccess = await GetActiveTenancyIds(context, userProfileId, today)
                .Concat(GetOwnedTenancyIds(context, userProfileId))
                .AnyAsync(id => id == tenancyId, cancellationToken);

        if (!canAccess)
        {
            throw new KeyNotFoundException("The tenancy was not found.");
        }

        return await (
            from member in context.TenancyMembers.AsNoTracking()
            join profile in context.UserProfiles.AsNoTracking()
                on member.UserProfileId equals profile.Id
            where member.TenancyId == tenancyId &&
                  member.JoinedOn <= today &&
                  (!member.LeftOn.HasValue || member.LeftOn.Value >= today)
            orderby profile.DisplayName
            select new SharedBillParticipantOption(profile.Id, profile.DisplayName))
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

        var attachmentEntities = await context.BillAttachments
            .AsNoTracking()
            .Where(attachment => attachment.SharedBillId == bill.Id)
            .OrderByDescending(attachment => attachment.UploadedAtUtc)
            .ToArrayAsync(cancellationToken);

        var profileIds = bill.Shares
            .Select(share => share.UserProfileId)
            .Append(bill.CreatedByUserProfileId)
            .Append(bill.PaidByUserProfileId)
            .Concat(bill.Disputes.Select(
                dispute => dispute.RaisedByUserProfileId))
            .Concat(bill.Disputes
                .Where(dispute =>
                    dispute.ResolvedByUserProfileId.HasValue)
                .Select(dispute =>
                    dispute.ResolvedByUserProfileId!.Value))
            .Concat(attachmentEntities.Select(
                attachment => attachment.UploadedByUserProfileId))
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

        var attachments = attachmentEntities
            .Select(attachment => new BillAttachmentItem(
                attachment.Id,
                attachment.UploadedByUserProfileId,
                displayNames[attachment.UploadedByUserProfileId],
                attachment.OriginalFileName,
                attachment.ContentType,
                attachment.FileSizeBytes,
                attachment.UploadedAtUtc,
                SharedBillAccessPolicy.CanDeleteAttachment(
                    bill,
                    attachment,
                    access)))
            .ToArray();

        return new SharedBillDetails(
            bill.Id,
            bill.TenancyId,
            bill.CreatedByUserProfileId,
            displayNames[bill.CreatedByUserProfileId],
            bill.PaidByUserProfileId,
            displayNames[bill.PaidByUserProfileId],
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
            attachments,
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

    public async Task<Guid> CreateAsync(
        CreateSharedBillCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(userProfileId, nameof(userProfileId));

        if (command.SplitMethod is not
            (BillSplitMethod.Equal or BillSplitMethod.FixedAmount or BillSplitMethod.Percentage))
        {
            throw new ArgumentException("The selected split method is not supported.", nameof(command));
        }

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        var tenancy = await context.Tenancies
            .Include(item => item.Members)
            .SingleOrDefaultAsync(item => item.Id == command.TenancyId, cancellationToken)
            ?? throw new KeyNotFoundException("The tenancy was not found.");
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

        if (!tenancy.IsActiveOn(today) || !SharedBillAccessPolicy.CanCreate(access))
        {
            throw new KeyNotFoundException("The tenancy was not found.");
        }

        if (activeMemberIds.Length == 0)
        {
            throw new InvalidOperationException("The tenancy has no active members to share this bill.");
        }

        if (!activeMemberIds.Contains(command.PaidByUserProfileId) &&
            !ownerIds.Contains(command.PaidByUserProfileId))
        {
            throw new ArgumentException(
                "The paying user must be an active member or property owner.",
                nameof(command));
        }

        var allocations = command.Allocations ?? [];
        if (command.SplitMethod != BillSplitMethod.Equal)
        {
            var allocationIds = allocations.Select(item => item.UserProfileId).ToArray();
            if (allocationIds.Length != activeMemberIds.Length ||
                allocationIds.Distinct().Count() != allocationIds.Length ||
                activeMemberIds.Any(id => !allocationIds.Contains(id)))
            {
                throw new ArgumentException(
                    "Enter one allocation for every active tenant.",
                    nameof(command));
            }
        }

        var now = GetUtcNow();
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

        switch (command.SplitMethod)
        {
            case BillSplitMethod.Equal:
                bill.AllocateEqually(
                    activeMemberIds.Select(id => (Guid.NewGuid(), id)),
                    command.CurrencyDecimalPlaces,
                    now);
                break;

            case BillSplitMethod.Percentage:
                if (allocations.Any(item => item.Value <= 0) ||
                    allocations.Sum(item => item.Value) != 100m)
                {
                    throw new ArgumentException(
                        "Percentages must be positive and add up to 100%.",
                        nameof(command));
                }

                bill.AllocateProportionally(
                    allocations.Select(item =>
                        (Guid.NewGuid(), item.UserProfileId, item.Value)),
                    BillSplitMethod.Percentage,
                    command.CurrencyDecimalPlaces,
                    now);
                break;

            case BillSplitMethod.FixedAmount:
                bill.AllocateFixed(
                    allocations.Select(item =>
                        (Guid.NewGuid(),
                         item.UserProfileId,
                         new Money(item.Value, command.Currency))),
                    now);
                break;
        }

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

    public async Task<Guid> AddAttachmentAsync(
        AddBillAttachmentCommand command,
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

        Demand(SharedBillAccessPolicy.CanAddAttachment(bill, access));

        var contentType = NormalizeAttachmentContentType(command.ContentType);
        await using var bufferedContent = await BufferAndValidateAttachmentAsync(
            command.Content,
            contentType,
            cancellationToken);

        var attachmentId = Guid.NewGuid();
        var storageKey = CreateAttachmentStorageKey(
            bill.Id,
            attachmentId,
            contentType);
        var attachment = new BillAttachment(
            attachmentId,
            bill.Id,
            userProfileId,
            command.OriginalFileName,
            contentType,
            bufferedContent.Length,
            storageKey,
            GetUtcNow());

        var fileWasStored = false;

        try
        {
            await attachmentStorage.SaveAsync(
                storageKey,
                bufferedContent,
                cancellationToken);
            fileWasStored = true;

            context.BillAttachments.Add(attachment);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            var entry = context.Entry(attachment);
            if (entry.State != EntityState.Detached)
            {
                entry.State = EntityState.Detached;
            }

            if (fileWasStored)
            {
                try
                {
                    await attachmentStorage.DeleteAsync(
                        storageKey,
                        CancellationToken.None);
                }
                catch
                {
                    // Preserve the database failure. A cleanup job can remove an orphan.
                }
            }

            throw;
        }

        return attachmentId;
    }

    public async Task<BillAttachmentDownload?> OpenAttachmentAsync(
        Guid attachmentId,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        EnsureId(attachmentId, nameof(attachmentId));
        EnsureId(userProfileId, nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        var attachment = await context.BillAttachments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == attachmentId,
                cancellationToken);

        if (attachment is null)
        {
            return null;
        }

        var bill = await context.SharedBills
            .AsNoTracking()
            .Include(item => item.Shares)
            .SingleOrDefaultAsync(
                item => item.Id == attachment.SharedBillId,
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

        var content = await attachmentStorage.OpenReadAsync(
            attachment.StorageKey,
            cancellationToken);

        return content is null
            ? null
            : new BillAttachmentDownload(
                attachment.OriginalFileName,
                attachment.ContentType,
                content);
    }

    public async Task DeleteAttachmentAsync(
        DeleteBillAttachmentCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(userProfileId, nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        var attachment = await context.BillAttachments
            .SingleOrDefaultAsync(
                item => item.Id == command.AttachmentId,
                cancellationToken)
            ?? throw new KeyNotFoundException("The shared bill was not found.");
        var bill = await GetBillAsync(
            context,
            attachment.SharedBillId,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            context,
            bill,
            userProfileId,
            cancellationToken);

        Demand(SharedBillAccessPolicy.CanDeleteAttachment(bill, attachment, access));

        context.BillAttachments.Remove(attachment);
        await context.SaveChangesAsync(cancellationToken);
        await attachmentStorage.DeleteAsync(
            attachment.StorageKey,
            cancellationToken);
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
            SharedBillAccessPolicy.CanAddAttachment(bill, access),
            SharedBillAccessPolicy.CanPublish(bill, access),
            SharedBillAccessPolicy.CanVoid(bill, access),
            SharedBillAccessPolicy.CanDeclarePayment(bill, access),
            SharedBillAccessPolicy.CanConfirmPayment(bill, access),
            SharedBillAccessPolicy.CanRaiseDispute(bill, access),
            SharedBillAccessPolicy.CanResolveDispute(bill, access));
    }

    private static async Task<MemoryStream> BufferAndValidateAttachmentAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanRead)
        {
            throw new ArgumentException(
                "The attachment stream must be readable.",
                nameof(content));
        }

        var bufferedContent = new MemoryStream();
        var buffer = new byte[81920];

        try
        {
            while (true)
            {
                var bytesRead = await content.ReadAsync(
                    buffer.AsMemory(),
                    cancellationToken);

                if (bytesRead == 0)
                {
                    break;
                }

                if (bufferedContent.Length + bytesRead > BillAttachment.MaxFileSizeBytes)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(content),
                        "Attachment size cannot exceed 10 MB.");
                }

                await bufferedContent.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken);
            }

            if (bufferedContent.Length == 0)
            {
                throw new ArgumentException(
                    "The attachment cannot be empty.",
                    nameof(content));
            }

            if (!bufferedContent.TryGetBuffer(out var contentBuffer))
            {
                throw new InvalidOperationException(
                    "The attachment buffer could not be inspected.");
            }

            var headerLength = (int)Math.Min(12, bufferedContent.Length);
            if (!HasValidAttachmentSignature(
                    contentType,
                    contentBuffer.AsSpan(0, headerLength)))
            {
                throw new ArgumentException(
                    "The file content does not match its declared type.",
                    nameof(content));
            }

            bufferedContent.Position = 0;
            return bufferedContent;
        }
        catch
        {
            await bufferedContent.DisposeAsync();
            throw;
        }
    }

    private static string NormalizeAttachmentContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type is required.", nameof(contentType));
        }

        var normalized = contentType.Trim().ToLowerInvariant();
        if (normalized is not
            ("image/jpeg" or "image/png" or "image/webp" or "application/pdf"))
        {
            throw new ArgumentException(
                "Only JPEG, PNG, WebP, and PDF files are supported.",
                nameof(contentType));
        }

        return normalized;
    }

    private static bool HasValidAttachmentSignature(
        string contentType,
        ReadOnlySpan<byte> header)
    {
        return contentType switch
        {
            "image/jpeg" =>
                header.Length >= 3 &&
                header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/png" =>
                header.Length >= 8 &&
                header[..8].SequenceEqual(new byte[]
                {
                    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
                }),
            "image/webp" =>
                header.Length >= 12 &&
                header[..4].SequenceEqual("RIFF"u8) &&
                header[8..12].SequenceEqual("WEBP"u8),
            "application/pdf" =>
                header.Length >= 5 && header[..5].SequenceEqual("%PDF-"u8),
            _ => false
        };
    }

    private static string CreateAttachmentStorageKey(
        Guid sharedBillId,
        Guid attachmentId,
        string contentType)
    {
        var extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "application/pdf" => ".pdf",
            _ => throw new ArgumentOutOfRangeException(nameof(contentType))
        };

        return $"bills/{sharedBillId:N}/{attachmentId:N}{extension}";
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
