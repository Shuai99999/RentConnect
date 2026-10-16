using RentConnect.Core.Enums;

namespace RentConnect.Core.Entities;

public sealed class MaintenanceStatusChange
{
    public const int MaxNoteLength = 1000;

    private MaintenanceStatusChange()
    {
    }

    internal MaintenanceStatusChange(
        Guid id,
        Guid maintenanceRequestId,
        MaintenanceStatus fromStatus,
        MaintenanceStatus toStatus,
        Guid changedByUserProfileId,
        DateTimeOffset changedAtUtc,
        string? note)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Status change ID cannot be empty.",
                nameof(id));
        }

        if (maintenanceRequestId == Guid.Empty)
        {
            throw new ArgumentException(
                "Maintenance request ID cannot be empty.",
                nameof(maintenanceRequestId));
        }

        if (changedByUserProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "The user profile ID cannot be empty.",
                nameof(changedByUserProfileId));
        }

        ValidateStatus(fromStatus, nameof(fromStatus));
        ValidateStatus(toStatus, nameof(toStatus));

        if (fromStatus == toStatus)
        {
            throw new ArgumentException(
                "The new status must be different from the previous status.",
                nameof(toStatus));
        }

        if (changedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The status change timestamp must use UTC.",
                nameof(changedAtUtc));
        }

        var normalizedNote =
            string.IsNullOrWhiteSpace(note)
                ? null
                : note.Trim();

        if (normalizedNote?.Length > MaxNoteLength)
        {
            throw new ArgumentException(
                $"Status change note cannot exceed {MaxNoteLength} characters.",
                nameof(note));
        }

        Id = id;
        MaintenanceRequestId = maintenanceRequestId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedByUserProfileId =
            changedByUserProfileId;
        ChangedAtUtc = changedAtUtc;
        Note = normalizedNote;
    }

    public Guid Id { get; private set; }

    public Guid MaintenanceRequestId { get; private set; }

    public MaintenanceStatus FromStatus
    {
        get;
        private set;
    }

    public MaintenanceStatus ToStatus
    {
        get;
        private set;
    }

    public Guid ChangedByUserProfileId
    {
        get;
        private set;
    }

    public DateTimeOffset ChangedAtUtc
    {
        get;
        private set;
    }

    public string? Note { get; private set; }

    private static void ValidateStatus(
        MaintenanceStatus status,
        string parameterName)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                status,
                "The maintenance status is invalid.");
        }
    }
}