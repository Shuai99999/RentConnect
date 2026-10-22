using RentConnect.Core.Enums;

namespace RentConnect.Core.Entities;

public sealed class MaintenanceRequest
{
    public const int MaxTitleLength = 150;
    public const int MaxDescriptionLength = 4000;
    public const int MaxResolutionNotesLength = 2000;

    private readonly List<MaintenanceStatusChange>
    _statusHistory = [];

    private MaintenanceRequest()
    {
    }

    public MaintenanceRequest(
        Guid id,
        Guid tenancyId,
        Guid createdByUserProfileId,
        string title,
        string description,
        MaintenanceCategory category,
        MaintenancePriority priority,
        MaintenanceVisibility visibility,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Maintenance request ID cannot be empty.",
                nameof(id));
        }

        if (tenancyId == Guid.Empty)
        {
            throw new ArgumentException(
                "Tenancy ID cannot be empty.",
                nameof(tenancyId));
        }

        if (createdByUserProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "Creator user profile ID cannot be empty.",
                nameof(createdByUserProfileId));
        }

        ValidateEnum(category, nameof(category));
        ValidateEnum(priority, nameof(priority));
        ValidateEnum(visibility, nameof(visibility));

        Id = id;
        TenancyId = tenancyId;
        CreatedByUserProfileId =
            createdByUserProfileId;

        Title = NormalizeTitle(title);
        Description = NormalizeDescription(description);

        Category = category;
        Priority = priority;
        Visibility = visibility;
        Status = MaintenanceStatus.Submitted;

        CreatedAtUtc =
            RequireUtc(createdAtUtc, nameof(createdAtUtc));

        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TenancyId { get; private set; }

    public Guid CreatedByUserProfileId { get; private set; }

    public string Title { get; private set; } =
        string.Empty;

    public string Description { get; private set; } =
        string.Empty;

    public MaintenanceCategory Category { get; private set; }

    public MaintenancePriority Priority { get; private set; }

    public MaintenanceVisibility Visibility
    {
        get;
        private set;
    }

    public MaintenanceStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ScheduledForUtc
    {
        get;
        private set;
    }

    public string? ResolutionNotes { get; private set; }

    public IReadOnlyCollection<MaintenanceStatusChange>
    StatusHistory => _statusHistory;

    public void UpdateDetails(
        string title,
        string description,
        MaintenanceCategory category,
        MaintenancePriority priority,
        MaintenanceVisibility visibility,
        DateTimeOffset changedAtUtc)
    {
        EnsureStatus(MaintenanceStatus.Submitted);

        ValidateEnum(category, nameof(category));
        ValidateEnum(priority, nameof(priority));
        ValidateEnum(visibility, nameof(visibility));

        var normalizedTitle =
            NormalizeTitle(title);

        var normalizedDescription =
            NormalizeDescription(description);

        var normalizedChangedTime =
            RequireValidChangeTime(changedAtUtc);

        Title = normalizedTitle;
        Description = normalizedDescription;
        Category = category;
        Priority = priority;
        Visibility = visibility;
        UpdatedAtUtc = normalizedChangedTime;
    }

    public void Acknowledge(
        Guid statusChangeId,
        Guid changedByUserProfileId,
        DateTimeOffset changedAtUtc,
        string? note = null)
    {
        EnsureStatus(MaintenanceStatus.Submitted);

        ChangeStatus(
            statusChangeId,
            changedByUserProfileId,
            MaintenanceStatus.Acknowledged,
            changedAtUtc,
            note);
    }

    public void Schedule(
    Guid statusChangeId,
    Guid changedByUserProfileId,
    DateTimeOffset scheduledForUtc,
    DateTimeOffset changedAtUtc,
    string? note = null)
    {
        EnsureStatus(MaintenanceStatus.Acknowledged);

        var normalizedScheduledTime =
            RequireUtc(
                scheduledForUtc,
                nameof(scheduledForUtc));

        var normalizedChangedTime =
            RequireValidChangeTime(changedAtUtc);

        if (normalizedScheduledTime <
            normalizedChangedTime)
        {
            throw new ArgumentException(
                "The scheduled time cannot be before the status change time.",
                nameof(scheduledForUtc));
        }

        var statusChange = CreateStatusChange(
            statusChangeId,
            changedByUserProfileId,
            MaintenanceStatus.Scheduled,
            normalizedChangedTime,
            note);

        ScheduledForUtc = normalizedScheduledTime;
        Status = MaintenanceStatus.Scheduled;
        UpdatedAtUtc = normalizedChangedTime;

        _statusHistory.Add(statusChange);
    }

    public void StartWork(
        Guid statusChangeId,
        Guid changedByUserProfileId,
        DateTimeOffset changedAtUtc,
        string? note = null)
    {
        EnsureStatus(
            MaintenanceStatus.Acknowledged,
            MaintenanceStatus.Scheduled);

        ChangeStatus(
            statusChangeId,
            changedByUserProfileId,
            MaintenanceStatus.InProgress,
            changedAtUtc,
            note);
    }

    public void Resolve(
        Guid statusChangeId,
        Guid changedByUserProfileId,
        string resolutionNotes,
        DateTimeOffset changedAtUtc)
    {
        EnsureStatus(MaintenanceStatus.InProgress);

        if (string.IsNullOrWhiteSpace(resolutionNotes))
        {
            throw new ArgumentException(
                "Resolution notes are required.",
                nameof(resolutionNotes));
        }

        var normalizedNotes = resolutionNotes.Trim();

        if (normalizedNotes.Length >
            MaxResolutionNotesLength)
        {
            throw new ArgumentException(
                $"Resolution notes cannot exceed {MaxResolutionNotesLength} characters.",
                nameof(resolutionNotes));
        }

        var normalizedChangedTime =
            RequireValidChangeTime(changedAtUtc);

        var statusChange = CreateStatusChange(
            statusChangeId,
            changedByUserProfileId,
            MaintenanceStatus.Resolved,
            normalizedChangedTime,
            normalizedNotes);

        ResolutionNotes = normalizedNotes;
        Status = MaintenanceStatus.Resolved;
        UpdatedAtUtc = normalizedChangedTime;

        _statusHistory.Add(statusChange);
    }

    public void Close(
        Guid statusChangeId,
        Guid changedByUserProfileId,
        DateTimeOffset changedAtUtc,
        string? note = null)
    {
        EnsureStatus(MaintenanceStatus.Resolved);

        ChangeStatus(
            statusChangeId,
            changedByUserProfileId,
            MaintenanceStatus.Closed,
            changedAtUtc,
            note);
    }

    public void Reopen(
        Guid statusChangeId,
        Guid changedByUserProfileId,
        string reason,
        DateTimeOffset changedAtUtc)
    {
        EnsureStatus(
            MaintenanceStatus.Resolved,
            MaintenanceStatus.Closed);

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "A reason is required to reopen a maintenance request.",
                nameof(reason));
        }

        var normalizedReason = reason.Trim();

        if (normalizedReason.Length >
            MaintenanceStatusChange.MaxNoteLength)
        {
            throw new ArgumentException(
                $"The reopen reason cannot exceed {MaintenanceStatusChange.MaxNoteLength} characters.",
                nameof(reason));
        }

        var normalizedChangedTime =
            RequireValidChangeTime(changedAtUtc);

        var statusChange = CreateStatusChange(
            statusChangeId,
            changedByUserProfileId,
            MaintenanceStatus.Acknowledged,
            normalizedChangedTime,
            normalizedReason);

        // A reopened request returns to triage. The previous resolution remains
        // available in status history, while current scheduling data is reset.
        ScheduledForUtc = null;
        ResolutionNotes = null;
        Status = MaintenanceStatus.Acknowledged;
        UpdatedAtUtc = normalizedChangedTime;

        _statusHistory.Add(statusChange);
    }

    public void Cancel(
        Guid statusChangeId,
        Guid changedByUserProfileId,
        DateTimeOffset changedAtUtc,
        string? note = null)
    {
        EnsureStatus(MaintenanceStatus.Submitted);

        ChangeStatus(
            statusChangeId,
            changedByUserProfileId,
            MaintenanceStatus.Cancelled,
            changedAtUtc,
            note);
    }

    private void ChangeStatus(
        Guid statusChangeId,
        Guid changedByUserProfileId,
        MaintenanceStatus newStatus,
        DateTimeOffset changedAtUtc,
        string? note)
    {
        var normalizedChangedTime =
            RequireValidChangeTime(changedAtUtc);

        var statusChange = CreateStatusChange(
            statusChangeId,
            changedByUserProfileId,
            newStatus,
            normalizedChangedTime,
            note);

        Status = newStatus;
        UpdatedAtUtc = normalizedChangedTime;

        _statusHistory.Add(statusChange);
    }

    private MaintenanceStatusChange CreateStatusChange(
        Guid statusChangeId,
        Guid changedByUserProfileId,
        MaintenanceStatus newStatus,
        DateTimeOffset changedAtUtc,
        string? note)
    {
        if (_statusHistory.Any(
            change => change.Id == statusChangeId))
        {
            throw new InvalidOperationException(
                "A status change with this ID already exists.");
        }

        return new MaintenanceStatusChange(
            statusChangeId,
            Id,
            Status,
            newStatus,
            changedByUserProfileId,
            changedAtUtc,
            note);
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException(
                "Maintenance request title is required.",
                nameof(title));
        }

        var normalizedTitle = title.Trim();

        if (normalizedTitle.Length > MaxTitleLength)
        {
            throw new ArgumentException(
                $"Maintenance request title cannot exceed {MaxTitleLength} characters.",
                nameof(title));
        }

        return normalizedTitle;
    }

    private static string NormalizeDescription(
        string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException(
                "Maintenance request description is required.",
                nameof(description));
        }

        var normalizedDescription =
            description.Trim();

        if (normalizedDescription.Length >
            MaxDescriptionLength)
        {
            throw new ArgumentException(
                $"Maintenance request description cannot exceed {MaxDescriptionLength} characters.",
                nameof(description));
        }

        return normalizedDescription;
    }

    private DateTimeOffset RequireValidChangeTime(
        DateTimeOffset changedAtUtc)
    {
        var normalizedTime =
            RequireUtc(
                changedAtUtc,
                nameof(changedAtUtc));

        if (normalizedTime < UpdatedAtUtc)
        {
            throw new ArgumentException(
                "The change time cannot be before the previous update.",
                nameof(changedAtUtc));
        }

        return normalizedTime;
    }

    private void EnsureStatus(
        params MaintenanceStatus[] allowedStatuses)
    {
        if (!allowedStatuses.Contains(Status))
        {
            throw new InvalidOperationException(
                $"Maintenance request cannot perform this operation while its status is {Status}.");
        }
    }

    private static DateTimeOffset RequireUtc(
        DateTimeOffset value,
        string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The timestamp must use UTC.",
                parameterName);
        }

        return value;
    }

    private static void ValidateEnum<TEnum>(
        TEnum value,
        string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The enum value is invalid.");
        }
    }
}
