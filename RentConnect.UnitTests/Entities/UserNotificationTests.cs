using RentConnect.Core.Entities;
using RentConnect.Core.Enums;

namespace RentConnect.UnitTests.Entities;

public class UserNotificationTests
{
    private static readonly DateTimeOffset
        CreatedAtUtc =
            new(
                2026,
                1,
                1,
                10,
                0,
                0,
                TimeSpan.Zero);

    [Fact]
    public void Constructor_WithValidValues_CreatesNotification()
    {
        var id = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var notification =
            new UserNotification(
                id,
                recipientId,
                actorId,
                NotificationType
                    .MaintenanceStatusChanged,
                "  Request updated  ",
                "  The request is now scheduled.  ",
                "  /maintenance/123  ",
                CreatedAtUtc);

        Assert.Equal(id, notification.Id);
        Assert.Equal(
            recipientId,
            notification.RecipientUserProfileId);
        Assert.Equal(
            actorId,
            notification.ActorUserProfileId);
        Assert.Equal(
            NotificationType
                .MaintenanceStatusChanged,
            notification.Type);
        Assert.Equal(
            "Request updated",
            notification.Title);
        Assert.Equal(
            "The request is now scheduled.",
            notification.Message);
        Assert.Equal(
            "/maintenance/123",
            notification.TargetPath);
        Assert.Equal(
            CreatedAtUtc,
            notification.CreatedAtUtc);
        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithoutTitle_Throws(
        string title)
    {
        Assert.Throws<ArgumentException>(
            () => CreateNotification(title: title));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("//example.com/path")]
    [InlineData("maintenance/123")]
    public void Constructor_WithNonLocalTargetPath_Throws(
        string targetPath)
    {
        Assert.Throws<ArgumentException>(
            () => CreateNotification(
                targetPath: targetPath));
    }

    [Fact]
    public void Constructor_WithEmptyActorId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CreateNotification(
                actorUserProfileId: Guid.Empty));
    }

    [Fact]
    public void Constructor_WithNonUtcTimestamp_Throws()
    {
        var nonUtcTimestamp =
            CreatedAtUtc.ToOffset(
                TimeSpan.FromHours(8));

        Assert.Throws<ArgumentException>(
            () => CreateNotification(
                createdAtUtc: nonUtcTimestamp));
    }

    [Fact]
    public void MarkAsRead_WithValidTime_MarksNotificationRead()
    {
        var notification = CreateNotification();
        var readAtUtc =
            CreatedAtUtc.AddMinutes(5);

        notification.MarkAsRead(readAtUtc);

        Assert.True(notification.IsRead);
        Assert.Equal(
            readAtUtc,
            notification.ReadAtUtc);
    }

    [Fact]
    public void MarkAsRead_BeforeCreation_Throws()
    {
        var notification = CreateNotification();

        Assert.Throws<ArgumentException>(
            () => notification.MarkAsRead(
                CreatedAtUtc.AddSeconds(-1)));

        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAtUtc);
    }

    [Fact]
    public void MarkAsRead_WhenAlreadyRead_PreservesFirstReadTime()
    {
        var notification = CreateNotification();
        var firstReadAtUtc =
            CreatedAtUtc.AddMinutes(5);

        notification.MarkAsRead(firstReadAtUtc);

        notification.MarkAsRead(
            CreatedAtUtc.AddMinutes(10));

        Assert.Equal(
            firstReadAtUtc,
            notification.ReadAtUtc);
    }

    private static UserNotification CreateNotification(
        Guid? actorUserProfileId = null,
        string title = "New maintenance update",
        string targetPath = "/maintenance/123",
        DateTimeOffset? createdAtUtc = null)
    {
        return new UserNotification(
            Guid.NewGuid(),
            Guid.NewGuid(),
            actorUserProfileId ?? Guid.NewGuid(),
            NotificationType.MaintenanceMessageAdded,
            title,
            "A new message was posted.",
            targetPath,
            createdAtUtc ?? CreatedAtUtc);
    }
}