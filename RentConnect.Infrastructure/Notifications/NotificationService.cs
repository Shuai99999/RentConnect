using Microsoft.EntityFrameworkCore;
using RentConnect.Application.Notifications;
using RentConnect.Data;

namespace RentConnect.Infrastructure.Notifications;

public sealed class NotificationService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TimeProvider timeProvider)
    : INotificationService
{
    public async Task<IReadOnlyList<UserNotificationItem>>
        GetRecentAsync(
            Guid userProfileId,
            int maximumCount = 50,
            CancellationToken cancellationToken = default)
    {
        EnsureId(
            userProfileId,
            nameof(userProfileId));

        if (maximumCount is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCount),
                "Notification count must be between 1 and 100.");
        }

        await using var context =
            await contextFactory.CreateDbContextAsync(
                cancellationToken);

        return await context.UserNotifications
            .AsNoTracking()
            .Where(notification =>
                notification.RecipientUserProfileId ==
                    userProfileId)
            .OrderByDescending(notification =>
                notification.CreatedAtUtc)
            .Take(maximumCount)
            .Select(notification =>
                new UserNotificationItem(
                    notification.Id,
                    notification.Type,
                    notification.Title,
                    notification.Message,
                    notification.TargetPath,
                    notification.CreatedAtUtc,
                    notification.ReadAtUtc,
                    notification.IsRead))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        EnsureId(
            userProfileId,
            nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(
                cancellationToken);

        return await context.UserNotifications
            .CountAsync(
                notification =>
                    notification.RecipientUserProfileId ==
                        userProfileId &&
                    notification.ReadAtUtc == null,
                cancellationToken);
    }

    public async Task MarkAsReadAsync(
        Guid notificationId,
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        EnsureId(
            notificationId,
            nameof(notificationId));

        EnsureId(
            userProfileId,
            nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(
                cancellationToken);

        var notification =
            await context.UserNotifications
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == notificationId &&
                        item.RecipientUserProfileId ==
                            userProfileId,
                    cancellationToken)
            ?? throw new KeyNotFoundException(
                "The notification was not found.");

        notification.MarkAsRead(
            timeProvider.GetUtcNow());

        await context.SaveChangesAsync(
            cancellationToken);
    }

    public async Task MarkAllAsReadAsync(
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        EnsureId(
            userProfileId,
            nameof(userProfileId));

        await using var context =
            await contextFactory.CreateDbContextAsync(
                cancellationToken);

        var notifications =
            await context.UserNotifications
                .Where(notification =>
                    notification.RecipientUserProfileId ==
                        userProfileId &&
                    notification.ReadAtUtc == null)
                .ToArrayAsync(cancellationToken);

        if (notifications.Length == 0)
        {
            return;
        }

        var readAtUtc =
            timeProvider.GetUtcNow();

        foreach (var notification in notifications)
        {
            notification.MarkAsRead(readAtUtc);
        }

        await context.SaveChangesAsync(
            cancellationToken);
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
