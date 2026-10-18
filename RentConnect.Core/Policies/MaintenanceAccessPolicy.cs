using RentConnect.Core.Entities;
using RentConnect.Core.Enums;

namespace RentConnect.Core.Policies;

public static class MaintenanceAccessPolicy
{
    public static bool CanView(
        MaintenanceRequest request,
        MaintenanceAccessContext access)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(access);

        if (access.IsPropertyOwner ||
            request.CreatedByUserProfileId == access.UserProfileId)
        {
            return true;
        }

        return request.Visibility ==
                   MaintenanceVisibility.SharedWithTenancy &&
               access.IsActiveTenancyMember;
    }

    public static bool CanPostMessage(
        MaintenanceRequest request,
        MaintenanceAccessContext access)
    {
        return CanView(request, access) &&
               (access.IsPropertyOwner ||
                access.IsActiveTenancyMember) &&
               request.Status is not MaintenanceStatus.Closed and
                   not MaintenanceStatus.Cancelled;
    }

    public static bool CanEditMessage(
        MaintenanceRequest request,
        MaintenanceMessage message,
        MaintenanceAccessContext access)
    {
        ArgumentNullException.ThrowIfNull(message);

        return CanPostMessage(request, access) &&
               message.MaintenanceRequestId == request.Id &&
               message.AuthorUserProfileId == access.UserProfileId &&
               !message.IsDeleted;
    }

    public static bool CanDeleteMessage(
        MaintenanceRequest request,
        MaintenanceMessage message,
        MaintenanceAccessContext access)
    {
        ArgumentNullException.ThrowIfNull(message);

        return CanPostMessage(request, access) &&
               message.MaintenanceRequestId == request.Id &&
               !message.IsDeleted &&
               (message.AuthorUserProfileId == access.UserProfileId ||
                access.IsPropertyOwner);
    }

    public static bool CanAddAttachment(
        MaintenanceRequest request,
        MaintenanceAccessContext access)
    {
        return CanView(request, access) &&
               (access.IsPropertyOwner ||
                access.IsActiveTenancyMember) &&
               request.Status is not MaintenanceStatus.Closed and
                   not MaintenanceStatus.Cancelled;
    }

    public static bool CanDeleteAttachment(
        MaintenanceRequest request,
        MaintenanceAttachment attachment,
        MaintenanceAccessContext access)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        return CanAddAttachment(request, access) &&
               attachment.MaintenanceRequestId == request.Id &&
               (attachment.UploadedByUserProfileId ==
                    access.UserProfileId ||
                access.IsPropertyOwner);
    }

    public static bool CanUpdateDetails(
        MaintenanceRequest request,
        MaintenanceAccessContext access)
    {
        return IsActiveReporter(request, access) &&
               request.Status == MaintenanceStatus.Submitted;
    }

    public static bool CanCancel(
        MaintenanceRequest request,
        MaintenanceAccessContext access)
    {
        return IsActiveReporter(request, access) &&
               request.Status == MaintenanceStatus.Submitted;
    }

    public static bool CanAcknowledge(
        MaintenanceRequest request,
        MaintenanceAccessContext access)
    {
        return access.IsPropertyOwner &&
               request.Status == MaintenanceStatus.Submitted;
    }

    public static bool CanAssign(
        MaintenanceRequest request,
        MaintenanceAccessContext access)
    {
        return access.IsPropertyOwner &&
               request.Status is not MaintenanceStatus.Closed and
                   not MaintenanceStatus.Cancelled;
    }

    public static bool CanSchedule(
        MaintenanceRequest request,
        MaintenanceAccessContext access)
    {
        return access.IsPropertyOwner &&
               request.Status == MaintenanceStatus.Acknowledged;
    }

    public static bool CanStartWork(
        MaintenanceRequest request,
        MaintenanceAccessContext access)
    {
        return access.IsPropertyOwner &&
               request.Status is MaintenanceStatus.Acknowledged or
                   MaintenanceStatus.Scheduled;
    }

    public static bool CanResolve(
        MaintenanceRequest request,
        MaintenanceAccessContext access)
    {
        return access.IsPropertyOwner &&
               request.Status == MaintenanceStatus.InProgress;
    }

    public static bool CanClose(
        MaintenanceRequest request,
        MaintenanceAccessContext access)
    {
        return CanTenantActOnVisibleRequest(request, access) &&
               request.Status == MaintenanceStatus.Resolved;
    }

    public static bool CanReopen(
        MaintenanceRequest request,
        MaintenanceAccessContext access)
    {
        return CanTenantActOnVisibleRequest(request, access) &&
               request.Status is MaintenanceStatus.Resolved or
                   MaintenanceStatus.Closed;
    }

    private static bool IsActiveReporter(
        MaintenanceRequest request,
        MaintenanceAccessContext access)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(access);

        return access.IsActiveTenancyMember &&
               request.CreatedByUserProfileId == access.UserProfileId;
    }

    private static bool CanTenantActOnVisibleRequest(
        MaintenanceRequest request,
        MaintenanceAccessContext access)
    {
        return access.IsActiveTenancyMember &&
               CanView(request, access);
    }
}
