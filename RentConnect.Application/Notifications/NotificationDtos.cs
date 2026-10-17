using RentConnect.Core.Enums;

namespace RentConnect.Application.Notifications;

public sealed record UserNotificationItem(
    Guid Id,
    NotificationType Type,
    string Title,
    string Message,
    string TargetPath,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc,
    bool IsRead);