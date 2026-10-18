using RentConnect.Core.Entities;

namespace RentConnect.UnitTests;

public sealed class BillAttachmentTests
{
    [Fact]
    public void Constructor_NormalizesSafeMetadata()
    {
        var attachment = new BillAttachment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            @"C:\fakepath\receipt.PDF",
            " Application/PDF ",
            1024,
            "bills/example/receipt.pdf",
            DateTimeOffset.UtcNow);

        Assert.Equal("receipt.PDF", attachment.OriginalFileName);
        Assert.Equal("application/pdf", attachment.ContentType);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("image/svg+xml")]
    [InlineData("application/octet-stream")]
    public void Constructor_RejectsUnsupportedContentType(string contentType)
    {
        Assert.Throws<ArgumentException>(() => new BillAttachment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "receipt.txt",
            contentType,
            1,
            "bills/example/receipt.txt",
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_RejectsOversizedFile()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BillAttachment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "receipt.pdf",
            "application/pdf",
            BillAttachment.MaxFileSizeBytes + 1,
            "bills/example/receipt.pdf",
            DateTimeOffset.UtcNow));
    }
}
