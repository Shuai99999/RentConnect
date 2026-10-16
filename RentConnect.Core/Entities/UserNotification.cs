using RentConnect.Core.Enums;

namespace RentConnect.Core.Entities;

public sealed class UserNotification
{
    public const int MaxTitleLength = 160;
    public const int MaxMessageLength = 500;
    public const int MaxTargetPathLength = 500;

    private UserNotification()
    {
    }

    public UserNotification(
        Guid id,
        Guid recipientUserProfileId,
        Guid? actorUserProfileId,
        NotificationType type,
        string title,
        string message,
        string targetPath,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Notification ID cannot be empty.",
                nameof(id));
        }

        if (recipientUserProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "Recipient user profile ID cannot be empty.",
                nameof(recipientUserProfileId));
        }

        if (actorUserProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "Actor user profile ID cannot be empty.",
                nameof(actorUserProfileId));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "The notification type is invalid.");
        }

        Id = id;
        RecipientUserProfileId =
            recipientUserProfileId;
        ActorUserProfileId =
            actorUserProfileId;
        Type = type;

        Title = NormalizeRequiredText(
            title,
            MaxTitleLength,
            nameof(title));

        Message = NormalizeRequiredText(
            message,
            MaxMessageLength,
            nameof(message));

        TargetPath = NormalizeTargetPath(targetPath);
        CreatedAtUtc = RequireUtc(
            createdAtUtc,
            nameof(createdAtUtc));
    }

    public Guid Id { get; private set; }

    public Guid RecipientUserProfileId
    {
        get;
        private set;
    }

    public Guid? ActorUserProfileId
    {
        get;
        private set;
    }

    public NotificationType Type { get; private set; }

    public string Title { get; private set; } =
        string.Empty;

    public string Message { get; private set; } =
        string.Empty;

    public string TargetPath { get; private set; } =
        string.Empty;

    public DateTimeOffset CreatedAtUtc
    {
        get;
        private set;
    }

    public DateTimeOffset? ReadAtUtc
    {
        get;
        private set;
    }

    public bool IsRead => ReadAtUtc.HasValue;

    public void MarkAsRead(DateTimeOffset readAtUtc)
    {
        var normalizedReadTime =
            RequireUtc(
                readAtUtc,
                nameof(readAtUtc));

        if (normalizedReadTime < CreatedAtUtc)
        {
            throw new ArgumentException(
                "Read time cannot be before creation time.",
                nameof(readAtUtc));
        }

        if (IsRead)
        {
            return;
        }

        ReadAtUtc = normalizedReadTime;
    }

    private static string NormalizeRequiredText(
        string value,
        int maxLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "A value is required.",
                parameterName);
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > maxLength)
        {
            throw new ArgumentException(
                $"The value cannot exceed {maxLength} characters.",
                parameterName);
        }

        return normalizedValue;
    }

    private static string NormalizeTargetPath(
        string targetPath)
    {
        var normalizedPath =
            NormalizeRequiredText(
                targetPath,
                MaxTargetPathLength,
                nameof(targetPath));

        if (!normalizedPath.StartsWith('/') ||
            normalizedPath.StartsWith("//"))
        {
            throw new ArgumentException(
                "Target path must be a local application path.",
                nameof(targetPath));
        }

        return normalizedPath;
    }

    private static DateTimeOffset RequireUtc(
        DateTimeOffset timestamp,
        string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The timestamp must use UTC.",
                parameterName);
        }

        return timestamp;
    }
}