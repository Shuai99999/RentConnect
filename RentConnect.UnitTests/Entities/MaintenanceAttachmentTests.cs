using RentConnect.Core.Entities;

namespace RentConnect.UnitTests.Entities;

public class MaintenanceAttachmentTests
{
    private static readonly DateTimeOffset
        UploadedAtUtc =
            new(
                2026,
                1,
                1,
                10,
                0,
                0,
                TimeSpan.Zero);

    [Fact]
    public void Constructor_WithValidValues_CreatesAttachment()
    {
        var id = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();

        var attachment =
            new MaintenanceAttachment(
                id,
                requestId,
                uploaderId,
                @" C:\fakepath\leak-photo.jpg ",
                " IMAGE/JPEG ",
                2048,
                " maintenance/test/photo.jpg ",
                UploadedAtUtc);

        Assert.Equal(id, attachment.Id);
        Assert.Equal(
            requestId,
            attachment.MaintenanceRequestId);
        Assert.Equal(
            uploaderId,
            attachment.UploadedByUserProfileId);
        Assert.Equal(
            "leak-photo.jpg",
            attachment.OriginalFileName);
        Assert.Equal(
            "image/jpeg",
            attachment.ContentType);
        Assert.Equal(
            2048,
            attachment.FileSizeBytes);
        Assert.Equal(
            "maintenance/test/photo.jpg",
            attachment.StorageKey);
        Assert.Equal(
            UploadedAtUtc,
            attachment.UploadedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"C:\fakepath\")]
    public void Constructor_WithoutValidFileName_Throws(
        string fileName)
    {
        Assert.Throws<ArgumentException>(
            () => CreateAttachment(
                originalFileName: fileName));
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("image/gif")]
    public void Constructor_WithUnsupportedContentType_Throws(
        string contentType)
    {
        Assert.Throws<ArgumentException>(
            () => CreateAttachment(
                contentType: contentType));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveFileSize_Throws(
        long fileSizeBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateAttachment(
                fileSizeBytes: fileSizeBytes));
    }

    [Fact]
    public void Constructor_WithOversizedFile_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateAttachment(
                fileSizeBytes:
                    MaintenanceAttachment
                        .MaxFileSizeBytes + 1));
    }

    [Fact]
    public void Constructor_WithNonUtcTimestamp_Throws()
    {
        var nonUtcTimestamp =
            UploadedAtUtc.ToOffset(
                TimeSpan.FromHours(8));

        Assert.Throws<ArgumentException>(
            () => CreateAttachment(
                uploadedAtUtc:
                    nonUtcTimestamp));
    }

    [Fact]
    public void Constructor_WithEmptyStorageKey_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CreateAttachment(
                storageKey: "   "));
    }

    private static MaintenanceAttachment
        CreateAttachment(
            string originalFileName = "photo.png",
            string contentType = "image/png",
            long fileSizeBytes = 1024,
            string storageKey =
                "maintenance/test/photo.png",
            DateTimeOffset? uploadedAtUtc = null)
    {
        return new MaintenanceAttachment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            originalFileName,
            contentType,
            fileSizeBytes,
            storageKey,
            uploadedAtUtc ?? UploadedAtUtc);
    }
}