using RentConnect.Core.Entities;

namespace RentConnect.UnitTests.Entities;

public sealed class RentalUnitImageTests
{
    private static readonly byte[] Png =
        [137, 80, 78, 71, 13, 10, 26, 10, 0];

    [Fact]
    public void Constructor_AssignsImageToRentalUnitAndCopiesContent()
    {
        var rentalUnitId = Guid.NewGuid();
        var uploadedAt = new DateTimeOffset(
            2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        var content = Png.ToArray();

        var image = new RentalUnitImage(rentalUnitId, content, uploadedAt);
        content[0] = 0;

        Assert.Equal(rentalUnitId, image.RentalUnitId);
        Assert.Equal("image/png", image.ContentType);
        Assert.Equal(Png, image.Content);
        Assert.Equal(uploadedAt, image.UploadedAtUtc);
    }

    [Fact]
    public void Constructor_RejectsMissingRentalUnit()
    {
        Assert.Throws<ArgumentException>(() =>
            new RentalUnitImage(Guid.Empty, Png, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_RejectsContentWithoutSupportedSignature()
    {
        Assert.Throws<ArgumentException>(() =>
            new RentalUnitImage(
                Guid.NewGuid(),
                [1, 2, 3, 4],
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_RejectsImageLargerThanFiveMegabytes()
    {
        var content = new byte[RentalUnitImage.MaxBytes + 1];
        Png.CopyTo(content, 0);

        Assert.Throws<ArgumentException>(() =>
            new RentalUnitImage(
                Guid.NewGuid(),
                content,
                DateTimeOffset.UtcNow));
    }
}
