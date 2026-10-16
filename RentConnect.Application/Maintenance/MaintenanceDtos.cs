using RentConnect.Core.Enums;

namespace RentConnect.Application.Maintenance;

public sealed record MaintenanceTenancyOption(
    Guid TenancyId,
    string PropertyName,
    string RentalUnitName);

public sealed record MaintenanceRequestSummary(
    Guid Id,
    Guid TenancyId,
    string Title,
    MaintenanceCategory Category,
    MaintenancePriority Priority,
    MaintenanceVisibility Visibility,
    MaintenanceStatus Status,
    DateTimeOffset UpdatedAtUtc);

public sealed record MaintenanceStatusChangeItem(
    Guid Id,
    MaintenanceStatus FromStatus,
    MaintenanceStatus ToStatus,
    Guid ChangedByUserProfileId,
    DateTimeOffset ChangedAtUtc,
    string? Note);

public sealed record MaintenanceMessageItem(
    Guid Id,
    Guid AuthorUserProfileId,
    string? Body,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? EditedAtUtc,
    bool IsDeleted,
    Guid? DeletedByUserProfileId,
    DateTimeOffset? DeletedAtUtc,
    bool CanEdit,
    bool CanDelete);

public sealed record MaintenancePermissions(
    bool CanPostMessage,
    bool CanUpdateDetails,
    bool CanCancel,
    bool CanAcknowledge,
    bool CanSchedule,
    bool CanStartWork,
    bool CanResolve,
    bool CanClose,
    bool CanReopen);

public sealed record MaintenanceRequestDetails(
    Guid Id,
    Guid TenancyId,
    Guid CreatedByUserProfileId,
    string Title,
    string Description,
    MaintenanceCategory Category,
    MaintenancePriority Priority,
    MaintenanceVisibility Visibility,
    MaintenanceStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ScheduledForUtc,
    string? ResolutionNotes,
    IReadOnlyList<MaintenanceStatusChangeItem> StatusHistory,
    IReadOnlyList<MaintenanceMessageItem> Messages,
    MaintenancePermissions Permissions);
