using Microsoft.EntityFrameworkCore;
using RentConnect.Application.Maintenance;
using RentConnect.Core.Entities;
using RentConnect.Core.Enums;
using RentConnect.Core.Policies;
using RentConnect.Data;

namespace RentConnect.Infrastructure.Maintenance;

public sealed class MaintenanceService(
    ApplicationDbContext context,
    TimeProvider timeProvider)
    : IMaintenanceService
{
    public async Task<IReadOnlyList<MaintenanceTenancyOption>>
        GetAvailableTenanciesAsync(
            Guid userProfileId,
            CancellationToken cancellationToken = default)
    {
        EnsureUserProfileId(userProfileId);

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
            select new MaintenanceTenancyOption(
                tenancy.Id,
                property.Name,
                unit.Name))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MaintenanceRequestSummary>>
        GetVisibleRequestsAsync(
            Guid userProfileId,
            CancellationToken cancellationToken = default)
    {
        EnsureUserProfileId(userProfileId);

        var today = GetToday();

        var activeTenancyIds =
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

        var ownedTenancyIds =
            from tenancy in context.Tenancies
            join unit in context.RentalUnits
                on tenancy.RentalUnitId equals unit.Id
            join owner in context.PropertyOwners
                on unit.PropertyId equals owner.PropertyId
            where owner.UserProfileId == userProfileId
            select tenancy.Id;

        return await context.MaintenanceRequests
            .AsNoTracking()
            .Where(request =>
                request.CreatedByUserProfileId == userProfileId ||
                ownedTenancyIds.Contains(request.TenancyId) ||
                request.Visibility ==
                    MaintenanceVisibility.SharedWithTenancy &&
                activeTenancyIds.Contains(request.TenancyId))
            .OrderByDescending(request => request.UpdatedAtUtc)
            .Select(request => new MaintenanceRequestSummary(
                request.Id,
                request.TenancyId,
                request.Title,
                request.Category,
                request.Priority,
                request.Visibility,
                request.Status,
                request.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<MaintenanceRequestDetails?> GetDetailsAsync(
        Guid maintenanceRequestId,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        EnsureId(
            maintenanceRequestId,
            nameof(maintenanceRequestId));
        EnsureUserProfileId(userProfileId);

        var request = await context.MaintenanceRequests
            .AsNoTracking()
            .Include(item => item.StatusHistory)
            .SingleOrDefaultAsync(
                item => item.Id == maintenanceRequestId,
                cancellationToken);

        if (request is null)
        {
            return null;
        }

        var access = await CreateAccessContextAsync(
            request,
            userProfileId,
            cancellationToken);

        // Returning null for both missing and forbidden records prevents callers
        // from discovering the existence of private maintenance requests.
        if (!MaintenanceAccessPolicy.CanView(request, access))
        {
            return null;
        }

        var messages = await context.MaintenanceMessages
            .AsNoTracking()
            .Where(message =>
                message.MaintenanceRequestId == request.Id)
            .OrderBy(message => message.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);

        var statusHistory = request.StatusHistory
            .OrderBy(change => change.ChangedAtUtc)
            .Select(change => new MaintenanceStatusChangeItem(
                change.Id,
                change.FromStatus,
                change.ToStatus,
                change.ChangedByUserProfileId,
                change.ChangedAtUtc,
                change.Note))
            .ToArray();

        var messageItems = messages
            .Select(message => new MaintenanceMessageItem(
                message.Id,
                message.AuthorUserProfileId,
                message.IsDeleted ? null : message.Body,
                message.CreatedAtUtc,
                message.EditedAtUtc,
                message.IsDeleted,
                message.DeletedByUserProfileId,
                message.DeletedAtUtc,
                MaintenanceAccessPolicy.CanEditMessage(
                    request,
                    message,
                    access),
                MaintenanceAccessPolicy.CanDeleteMessage(
                    request,
                    message,
                    access)))
            .ToArray();

        return new MaintenanceRequestDetails(
            request.Id,
            request.TenancyId,
            request.CreatedByUserProfileId,
            request.Title,
            request.Description,
            request.Category,
            request.Priority,
            request.Visibility,
            request.Status,
            request.CreatedAtUtc,
            request.UpdatedAtUtc,
            request.ScheduledForUtc,
            request.ResolutionNotes,
            statusHistory,
            messageItems,
            CreatePermissions(request, access));
    }

    public async Task<Guid> CreateAsync(
        CreateMaintenanceRequestCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureUserProfileId(userProfileId);

        var tenancy = await context.Tenancies
            .Include(item => item.Members)
            .SingleOrDefaultAsync(
                item => item.Id == command.TenancyId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "The tenancy was not found.");

        var today = GetToday();
        var isActiveMember =
            tenancy.IsMemberActiveOn(userProfileId, today);
        var isOwner = await IsPropertyOwnerAsync(
            tenancy.Id,
            userProfileId,
            cancellationToken);

        if (!tenancy.IsActiveOn(today) ||
            !isActiveMember && !isOwner)
        {
            throw new KeyNotFoundException(
                "The tenancy was not found.");
        }

        var request = new MaintenanceRequest(
            Guid.NewGuid(),
            tenancy.Id,
            userProfileId,
            command.Title,
            command.Description,
            command.Category,
            command.Priority,
            command.Visibility,
            timeProvider.GetUtcNow());

        context.MaintenanceRequests.Add(request);
        await context.SaveChangesAsync(cancellationToken);

        return request.Id;
    }

    public async Task UpdateDetailsAsync(
        UpdateMaintenanceRequestCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureUserProfileId(userProfileId);

        var request = await GetRequestAsync(
            command.MaintenanceRequestId,
            includeStatusHistory: false,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            request,
            userProfileId,
            cancellationToken);

        Demand(
            MaintenanceAccessPolicy.CanUpdateDetails(request, access));

        request.UpdateDetails(
            command.Title,
            command.Description,
            command.Category,
            command.Priority,
            command.Visibility,
            timeProvider.GetUtcNow());

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> AddMessageAsync(
        AddMaintenanceMessageCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureUserProfileId(userProfileId);

        var request = await GetRequestAsync(
            command.MaintenanceRequestId,
            includeStatusHistory: false,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            request,
            userProfileId,
            cancellationToken);

        Demand(
            MaintenanceAccessPolicy.CanPostMessage(request, access));

        var message = new MaintenanceMessage(
            Guid.NewGuid(),
            request.Id,
            userProfileId,
            command.Body,
            timeProvider.GetUtcNow());

        context.MaintenanceMessages.Add(message);
        await context.SaveChangesAsync(cancellationToken);

        return message.Id;
    }

    public async Task EditMessageAsync(
        EditMaintenanceMessageCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureUserProfileId(userProfileId);

        var message = await GetMessageAsync(
            command.MessageId,
            cancellationToken);
        var request = await GetRequestAsync(
            message.MaintenanceRequestId,
            includeStatusHistory: false,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            request,
            userProfileId,
            cancellationToken);

        Demand(
            MaintenanceAccessPolicy.CanEditMessage(
                request,
                message,
                access));

        message.Edit(command.Body, timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteMessageAsync(
        DeleteMaintenanceMessageCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureUserProfileId(userProfileId);

        var message = await GetMessageAsync(
            command.MessageId,
            cancellationToken);
        var request = await GetRequestAsync(
            message.MaintenanceRequestId,
            includeStatusHistory: false,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            request,
            userProfileId,
            cancellationToken);

        Demand(
            MaintenanceAccessPolicy.CanDeleteMessage(
                request,
                message,
                access));

        message.Delete(userProfileId, timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangeStatusAsync(
        ChangeMaintenanceStatusCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureUserProfileId(userProfileId);

        var request = await GetRequestAsync(
            command.MaintenanceRequestId,
            includeStatusHistory: true,
            cancellationToken);
        var access = await CreateAccessContextAsync(
            request,
            userProfileId,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        var statusChangeId = Guid.NewGuid();

        switch (command.Action)
        {
            case MaintenanceAction.Acknowledge:
                Demand(
                    MaintenanceAccessPolicy.CanAcknowledge(
                        request,
                        access));
                request.Acknowledge(
                    statusChangeId,
                    userProfileId,
                    now,
                    command.Note);
                break;

            case MaintenanceAction.Schedule:
                Demand(
                    MaintenanceAccessPolicy.CanSchedule(
                        request,
                        access));
                request.Schedule(
                    statusChangeId,
                    userProfileId,
                    command.ScheduledForUtc ??
                        throw new ArgumentException(
                            "A scheduled time is required.",
                            nameof(command)),
                    now,
                    command.Note);
                break;

            case MaintenanceAction.StartWork:
                Demand(
                    MaintenanceAccessPolicy.CanStartWork(
                        request,
                        access));
                request.StartWork(
                    statusChangeId,
                    userProfileId,
                    now,
                    command.Note);
                break;

            case MaintenanceAction.Resolve:
                Demand(
                    MaintenanceAccessPolicy.CanResolve(
                        request,
                        access));
                request.Resolve(
                    statusChangeId,
                    userProfileId,
                    command.Note ?? string.Empty,
                    now);
                break;

            case MaintenanceAction.Close:
                Demand(
                    MaintenanceAccessPolicy.CanClose(
                        request,
                        access));
                request.Close(
                    statusChangeId,
                    userProfileId,
                    now,
                    command.Note);
                break;

            case MaintenanceAction.Reopen:
                Demand(
                    MaintenanceAccessPolicy.CanReopen(
                        request,
                        access));
                request.Reopen(
                    statusChangeId,
                    userProfileId,
                    command.Note ?? string.Empty,
                    now);
                break;

            case MaintenanceAction.Cancel:
                Demand(
                    MaintenanceAccessPolicy.CanCancel(
                        request,
                        access));
                request.Cancel(
                    statusChangeId,
                    userProfileId,
                    now,
                    command.Note);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(command),
                    command.Action,
                    "The maintenance action is invalid.");
        }

        var newStatusChange = request.StatusHistory.Single(
            change => change.Id == statusChangeId);

        // Status history uses application-assigned Guid keys. Explicitly marking
        // the new child as Added prevents EF from treating it as an update when
        // the aggregate was already tracked by an earlier operation in the scope.
        context.MaintenanceStatusChanges.Add(newStatusChange);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<MaintenanceRequest> GetRequestAsync(
        Guid maintenanceRequestId,
        bool includeStatusHistory,
        CancellationToken cancellationToken)
    {
        EnsureId(
            maintenanceRequestId,
            nameof(maintenanceRequestId));

        IQueryable<MaintenanceRequest> query =
            context.MaintenanceRequests;

        if (includeStatusHistory)
        {
            query = query.Include(item => item.StatusHistory);
        }

        return await query.SingleOrDefaultAsync(
                   item => item.Id == maintenanceRequestId,
                   cancellationToken)
               ?? throw new KeyNotFoundException(
                   "The maintenance resource was not found.");
    }

    private async Task<MaintenanceMessage> GetMessageAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        EnsureId(messageId, nameof(messageId));

        return await context.MaintenanceMessages
                   .SingleOrDefaultAsync(
                       message => message.Id == messageId,
                       cancellationToken)
               ?? throw new KeyNotFoundException(
                   "The maintenance resource was not found.");
    }

    private async Task<MaintenanceAccessContext>
        CreateAccessContextAsync(
            MaintenanceRequest request,
            Guid userProfileId,
            CancellationToken cancellationToken)
    {
        var today = GetToday();

        var isActiveMember = await context.Tenancies
            .Where(tenancy =>
                tenancy.Id == request.TenancyId &&
                !tenancy.IsCancelled &&
                tenancy.StartDate <= today &&
                (!tenancy.EndDate.HasValue ||
                 tenancy.EndDate.Value >= today))
            .AnyAsync(
                tenancy => context.TenancyMembers.Any(member =>
                    member.TenancyId == tenancy.Id &&
                    member.UserProfileId == userProfileId &&
                    member.JoinedOn <= today &&
                    (!member.LeftOn.HasValue ||
                     member.LeftOn.Value >= today)),
                cancellationToken);

        var isOwner = await IsPropertyOwnerAsync(
            request.TenancyId,
            userProfileId,
            cancellationToken);

        return new MaintenanceAccessContext(
            userProfileId,
            isOwner,
            isActiveMember);
    }

    private Task<bool> IsPropertyOwnerAsync(
        Guid tenancyId,
        Guid userProfileId,
        CancellationToken cancellationToken)
    {
        return (
            from tenancy in context.Tenancies
            join unit in context.RentalUnits
                on tenancy.RentalUnitId equals unit.Id
            join owner in context.PropertyOwners
                on unit.PropertyId equals owner.PropertyId
            where tenancy.Id == tenancyId &&
                  owner.UserProfileId == userProfileId
            select owner)
            .AnyAsync(cancellationToken);
    }

    private static MaintenancePermissions CreatePermissions(
        MaintenanceRequest request,
        MaintenanceAccessContext access)
    {
        return new MaintenancePermissions(
            MaintenanceAccessPolicy.CanPostMessage(request, access),
            MaintenanceAccessPolicy.CanUpdateDetails(request, access),
            MaintenanceAccessPolicy.CanCancel(request, access),
            MaintenanceAccessPolicy.CanAcknowledge(request, access),
            MaintenanceAccessPolicy.CanSchedule(request, access),
            MaintenanceAccessPolicy.CanStartWork(request, access),
            MaintenanceAccessPolicy.CanResolve(request, access),
            MaintenanceAccessPolicy.CanClose(request, access),
            MaintenanceAccessPolicy.CanReopen(request, access));
    }

    private DateOnly GetToday()
    {
        return DateOnly.FromDateTime(
            timeProvider.GetUtcNow().UtcDateTime);
    }

    private static void Demand(bool isAllowed)
    {
        if (!isAllowed)
        {
            // Missing and forbidden resources deliberately have the same public
            // failure shape so private requests cannot be discovered by ID.
            throw new KeyNotFoundException(
                "The maintenance resource was not found.");
        }
    }

    private static void EnsureUserProfileId(Guid userProfileId)
    {
        EnsureId(userProfileId, nameof(userProfileId));
    }

    private static void EnsureId(
        Guid id,
        string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "The ID cannot be empty.",
                parameterName);
        }
    }
}
