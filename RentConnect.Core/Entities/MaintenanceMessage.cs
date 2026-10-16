namespace RentConnect.Core.Entities;

public sealed class MaintenanceMessage
{
    public const int MaxBodyLength = 4000;

    private MaintenanceMessage()
    {
    }

    public MaintenanceMessage(
        Guid id,
        Guid maintenanceRequestId,
        Guid authorUserProfileId,
        string body,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Message ID cannot be empty.",
                nameof(id));
        }

        if (maintenanceRequestId == Guid.Empty)
        {
            throw new ArgumentException(
                "Maintenance request ID cannot be empty.",
                nameof(maintenanceRequestId));
        }

        if (authorUserProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "Author user profile ID cannot be empty.",
                nameof(authorUserProfileId));
        }

        Id = id;
        MaintenanceRequestId = maintenanceRequestId;
        AuthorUserProfileId = authorUserProfileId;
        Body = NormalizeBody(body);

        EnsureUtc(
            createdAtUtc,
            nameof(createdAtUtc));

        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid MaintenanceRequestId { get; private set; }

    public Guid AuthorUserProfileId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? EditedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public Guid? DeletedByUserProfileId { get; private set; }

    public bool IsEdited => EditedAtUtc.HasValue;

    public bool IsDeleted => DeletedAtUtc.HasValue;

    public void Edit(
        string body,
        DateTimeOffset editedAtUtc)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException(
                "A deleted message cannot be edited.");
        }

        EnsureUtc(
            editedAtUtc,
            nameof(editedAtUtc));

        EnsureAfterLastChange(
            editedAtUtc,
            nameof(editedAtUtc));

        var normalizedBody = NormalizeBody(body);

        if (normalizedBody == Body)
        {
            throw new ArgumentException(
                "The edited message must be different from the current message.",
                nameof(body));
        }

        Body = normalizedBody;
        EditedAtUtc = editedAtUtc;
    }

    public void Delete(
        Guid deletedByUserProfileId,
        DateTimeOffset deletedAtUtc)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException(
                "The message has already been deleted.");
        }

        if (deletedByUserProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "Deleted-by user profile ID cannot be empty.",
                nameof(deletedByUserProfileId));
        }

        EnsureUtc(
            deletedAtUtc,
            nameof(deletedAtUtc));

        EnsureAfterLastChange(
            deletedAtUtc,
            nameof(deletedAtUtc));

        DeletedByUserProfileId =
            deletedByUserProfileId;

        DeletedAtUtc = deletedAtUtc;
    }

    private static string NormalizeBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException(
                "Message body is required.",
                nameof(body));
        }

        var normalizedBody = body.Trim();

        if (normalizedBody.Length > MaxBodyLength)
        {
            throw new ArgumentException(
                $"Message body cannot exceed {MaxBodyLength} characters.",
                nameof(body));
        }

        return normalizedBody;
    }

    private static void EnsureUtc(
        DateTimeOffset timestamp,
        string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The timestamp must use UTC.",
                parameterName);
        }
    }

    private void EnsureAfterLastChange(
        DateTimeOffset timestamp,
        string parameterName)
    {
        var lastChangeAtUtc =
            EditedAtUtc ?? CreatedAtUtc;

        if (timestamp <= lastChangeAtUtc)
        {
            throw new ArgumentException(
                "The timestamp must be later than the previous change.",
                parameterName);
        }
    }
}