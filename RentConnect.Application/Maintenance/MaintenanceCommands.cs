using RentConnect.Core.Enums;

namespace RentConnect.Application.Maintenance;

public sealed record CreateMaintenanceRequestCommand(
    Guid TenancyId,
    string Title,
    string Description,
    MaintenanceCategory Category,
    MaintenancePriority Priority,
    MaintenanceVisibility Visibility);

public sealed record UpdateMaintenanceRequestCommand(
    Guid MaintenanceRequestId,
    string Title,
    string Description,
    MaintenanceCategory Category,
    MaintenancePriority Priority,
    MaintenanceVisibility Visibility);

public sealed record AddMaintenanceMessageCommand(
    Guid MaintenanceRequestId,
    string Body);

public sealed record EditMaintenanceMessageCommand(
    Guid MessageId,
    string Body);

public sealed record DeleteMaintenanceMessageCommand(
    Guid MessageId);

public sealed record ChangeMaintenanceStatusCommand(
    Guid MaintenanceRequestId,
    MaintenanceAction Action,
    DateTimeOffset? ScheduledForUtc = null,
    string? Note = null);

public sealed record AddMaintenanceAttachmentCommand(
    Guid MaintenanceRequestId,
    string OriginalFileName,
    string ContentType,
    Stream Content);

public sealed record DeleteMaintenanceAttachmentCommand(
    Guid AttachmentId);

public sealed record AssignMaintenanceRequestCommand(
    Guid MaintenanceRequestId,
    Guid? AssignedToUserProfileId);
