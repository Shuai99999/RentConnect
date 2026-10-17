namespace RentConnect.Application.Notifications;

public interface INotificationService
{
    Task<IReadOnlyList<UserNotificationItem>>
        GetRecentAsync(
            Guid userProfileId,
            int maximumCount = 50,
            CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(
        Guid notificationId,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(
        Guid userProfileId,
        CancellationToken cancellationToken = default);
}