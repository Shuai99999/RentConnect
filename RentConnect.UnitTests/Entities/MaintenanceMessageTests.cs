using RentConnect.Core.Entities;

namespace RentConnect.UnitTests.Entities;

public class MaintenanceMessageTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(
            2026,
            1,
            1,
            10,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Constructor_WithValidValues_CreatesMessage()
    {
        var id = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var message = new MaintenanceMessage(
            id,
            requestId,
            authorId,
            "  The plumber can visit tomorrow.  ",
            CreatedAtUtc);

        Assert.Equal(id, message.Id);
        Assert.Equal(
            requestId,
            message.MaintenanceRequestId);
        Assert.Equal(
            authorId,
            message.AuthorUserProfileId);
        Assert.Equal(
            "The plumber can visit tomorrow.",
            message.Body);
        Assert.Equal(
            CreatedAtUtc,
            message.CreatedAtUtc);

        Assert.False(message.IsEdited);
        Assert.False(message.IsDeleted);
        Assert.Null(message.EditedAtUtc);
        Assert.Null(message.DeletedAtUtc);
        Assert.Null(message.DeletedByUserProfileId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithoutBody_Throws(
        string body)
    {
        Assert.Throws<ArgumentException>(
            () => new MaintenanceMessage(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                body,
                CreatedAtUtc));
    }

    [Fact]
    public void Constructor_WithNonUtcTimestamp_Throws()
    {
        var nonUtcTimestamp =
            CreatedAtUtc.ToOffset(
                TimeSpan.FromHours(8));

        Assert.Throws<ArgumentException>(
            () => new MaintenanceMessage(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Message",
                nonUtcTimestamp));
    }

    [Fact]
    public void Edit_WithValidValues_UpdatesMessage()
    {
        var message = CreateMessage();
        var editedAtUtc =
            CreatedAtUtc.AddMinutes(10);

        message.Edit(
            "  Updated message.  ",
            editedAtUtc);

        Assert.Equal(
            "Updated message.",
            message.Body);
        Assert.True(message.IsEdited);
        Assert.Equal(
            editedAtUtc,
            message.EditedAtUtc);
        Assert.False(message.IsDeleted);
    }

    [Fact]
    public void Edit_WithSameBody_Throws()
    {
        var message = CreateMessage();

        Assert.Throws<ArgumentException>(
            () => message.Edit(
                "Initial message.",
                CreatedAtUtc.AddMinutes(10)));
    }

    [Fact]
    public void Delete_WithValidValues_SoftDeletesMessage()
    {
        var message = CreateMessage();
        var deletedByUserProfileId =
            Guid.NewGuid();
        var deletedAtUtc =
            CreatedAtUtc.AddMinutes(20);

        message.Delete(
            deletedByUserProfileId,
            deletedAtUtc);

        Assert.True(message.IsDeleted);
        Assert.Equal(
            deletedByUserProfileId,
            message.DeletedByUserProfileId);
        Assert.Equal(
            deletedAtUtc,
            message.DeletedAtUtc);

        // 软删除仍在数据库中保留原始正文。
        Assert.Equal(
            "Initial message.",
            message.Body);
    }

    [Fact]
    public void Edit_AfterDeletion_Throws()
    {
        var message = CreateMessage();

        message.Delete(
            Guid.NewGuid(),
            CreatedAtUtc.AddMinutes(10));

        Assert.Throws<InvalidOperationException>(
            () => message.Edit(
                "Updated message.",
                CreatedAtUtc.AddMinutes(20)));
    }

    [Fact]
    public void Delete_WhenAlreadyDeleted_Throws()
    {
        var message = CreateMessage();

        message.Delete(
            Guid.NewGuid(),
            CreatedAtUtc.AddMinutes(10));

        Assert.Throws<InvalidOperationException>(
            () => message.Delete(
                Guid.NewGuid(),
                CreatedAtUtc.AddMinutes(20)));
    }

    [Fact]
    public void Edit_WithTimestampBeforeCreation_Throws()
    {
        var message = CreateMessage();

        Assert.Throws<ArgumentException>(
            () => message.Edit(
                "Updated message.",
                CreatedAtUtc.AddMinutes(-1)));
    }

    private static MaintenanceMessage CreateMessage()
    {
        return new MaintenanceMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Initial message.",
            CreatedAtUtc);
    }
}