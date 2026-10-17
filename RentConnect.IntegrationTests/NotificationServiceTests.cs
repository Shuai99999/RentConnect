using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentConnect.Core.Entities;
using RentConnect.Core.Enums;
using RentConnect.Data;
using RentConnect.Infrastructure.Notifications;

namespace RentConnect.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class NotificationServiceTests
{
    [Fact]
    public async Task NotificationService_OnlyAccessesRecipientsNotifications()
    {
        var connectionString =
            CreateConnectionString();

        await using var serviceProvider =
            CreateServiceProvider(connectionString);

        await using var scope =
            serviceProvider.CreateAsyncScope();

        var context =
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var contextFactory =
            scope.ServiceProvider
                .GetRequiredService<
                    IDbContextFactory<ApplicationDbContext>>();

        var recipientId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var createdAtUtc =
            new DateTimeOffset(
                2026,
                1,
                1,
                10,
                0,
                0,
                TimeSpan.Zero);

        try
        {
            await context.Database.MigrateAsync();

            context.UserProfiles.AddRange(
                new UserProfile(
                    recipientId,
                    "Notification Recipient"),
                new UserProfile(
                    otherUserId,
                    "Other User"));

            var firstNotification =
                new UserNotification(
                    Guid.NewGuid(),
                    recipientId,
                    otherUserId,
                    NotificationType
                        .MaintenanceRequestCreated,
                    "New maintenance request",
                    "A maintenance request was created.",
                    "/maintenance/11111111-1111-1111-1111-111111111111",
                    createdAtUtc);

            var secondNotification =
                new UserNotification(
                    Guid.NewGuid(),
                    recipientId,
                    otherUserId,
                    NotificationType
                        .MaintenanceMessageAdded,
                    "New message",
                    "A new maintenance message was posted.",
                    "/maintenance/22222222-2222-2222-2222-222222222222",
                    createdAtUtc.AddMinutes(1));

            var thirdNotification =
                new UserNotification(
                    Guid.NewGuid(),
                    recipientId,
                    otherUserId,
                    NotificationType
                        .MaintenanceStatusChanged,
                    "Request updated",
                    "The request status changed.",
                    "/maintenance/33333333-3333-3333-3333-333333333333",
                    createdAtUtc.AddMinutes(2));

            firstNotification.MarkAsRead(
                createdAtUtc.AddMinutes(3));

            var otherUserNotification =
                new UserNotification(
                    Guid.NewGuid(),
                    otherUserId,
                    recipientId,
                    NotificationType
                        .MaintenanceAttachmentAdded,
                    "New photo",
                    "A new maintenance photo was added.",
                    "/maintenance/44444444-4444-4444-4444-444444444444",
                    createdAtUtc.AddMinutes(3));

            context.UserNotifications.AddRange(
                firstNotification,
                secondNotification,
                thirdNotification,
                otherUserNotification);

            await context.SaveChangesAsync();

            var testClock =
                new TestTimeProvider(
                    createdAtUtc.AddHours(1));

            var service =
                new NotificationService(
                    contextFactory,
                    testClock);

            var recentNotificationsTask =
                service.GetRecentAsync(
                    recipientId,
                    maximumCount: 2);

            var unreadCountTask =
                service.GetUnreadCountAsync(
                    recipientId);

            await Task.WhenAll(
                recentNotificationsTask,
                unreadCountTask);

            var recentNotifications =
                await recentNotificationsTask;

            Assert.Equal(
                2,
                recentNotifications.Count);

            Assert.Equal(
                thirdNotification.Id,
                recentNotifications[0].Id);

            Assert.Equal(
                secondNotification.Id,
                recentNotifications[1].Id);

            Assert.DoesNotContain(
                recentNotifications,
                notification =>
                    notification.Id ==
                        otherUserNotification.Id);

            Assert.Equal(2, await unreadCountTask);

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => service.MarkAsReadAsync(
                    thirdNotification.Id,
                    otherUserId));

            await service.MarkAsReadAsync(
                thirdNotification.Id,
                recipientId);

            Assert.Equal(
                1,
                await service.GetUnreadCountAsync(
                    recipientId));

            await service.MarkAllAsReadAsync(
                recipientId);

            Assert.Equal(
                0,
                await service.GetUnreadCountAsync(
                    recipientId));

            context.ChangeTracker.Clear();

            var recipientNotifications =
                await context.UserNotifications
                    .Where(notification =>
                        notification.RecipientUserProfileId ==
                            recipientId)
                    .ToArrayAsync();

            Assert.All(
                recipientNotifications,
                notification =>
                    Assert.True(notification.IsRead));

            var unchangedOtherNotification =
                await context.UserNotifications
                    .SingleAsync(notification =>
                        notification.Id ==
                            otherUserNotification.Id);

            Assert.False(
                unchangedOtherNotification.IsRead);

            await Assert.ThrowsAsync<
                ArgumentOutOfRangeException>(
                () => service.GetRecentAsync(
                    recipientId,
                    maximumCount: 101));
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static ServiceProvider CreateServiceProvider(
        string connectionString)
    {
        var services = new ServiceCollection();

        services.AddDbContextFactory<ApplicationDbContext>(
            options =>
                options.UseSqlServer(connectionString));

        services
            .AddIdentityCore<ApplicationUser>(
                options =>
                {
                    options.Stores.SchemaVersion =
                        IdentitySchemaVersions.Version3;
                })
            .AddEntityFrameworkStores<ApplicationDbContext>();

        return services.BuildServiceProvider();
    }

    private static string CreateConnectionString()
    {
        var baseConnectionString =
            Environment.GetEnvironmentVariable(
                "RENTCONNECT_TEST_SQLSERVER")
            ??
            "Server=(localdb)\\RentConnectLocalDB;" +
            "Database=master;" +
            "Trusted_Connection=True;" +
            "MultipleActiveResultSets=True;" +
            "TrustServerCertificate=True";

        var builder =
            new SqlConnectionStringBuilder(
                baseConnectionString)
            {
                InitialCatalog =
                    $"RentConnectNotificationTests_{Guid.NewGuid():N}"
            };

        return builder.ConnectionString;
    }

    private sealed class TestTimeProvider(
        DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
