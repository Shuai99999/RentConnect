namespace RentConnect.Core.Entities;

public sealed class RentalUnitImage
{
    public const int MaxBytes = 5 * 1024 * 1024;

    private RentalUnitImage()
    {
    }

    public RentalUnitImage(
        Guid rentalUnitId,
        byte[] content,
        DateTimeOffset uploadedAtUtc)
    {
        if (rentalUnitId == Guid.Empty)
        {
            throw new ArgumentException(
                "Rental unit is required.",
                nameof(rentalUnitId));
        }

        ArgumentNullException.ThrowIfNull(content);

        if (content.Length == 0 || content.Length > MaxBytes)
        {
            throw new ArgumentException(
                "Images must be between 1 byte and 5 MB.",
                nameof(content));
        }

        ContentType = DetectContentType(content);
        Id = Guid.NewGuid();
        RentalUnitId = rentalUnitId;
        Content = content.ToArray();
        UploadedAtUtc = uploadedAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public Guid RentalUnitId { get; private set; }

    public string ContentType { get; private set; } = string.Empty;

    public byte[] Content { get; private set; } = [];

    public DateTimeOffset UploadedAtUtc { get; private set; }

    private static string DetectContentType(byte[] bytes)
    {
        ReadOnlySpan<byte> pngSignature =
            [137, 80, 78, 71, 13, 10, 26, 10];

        if (bytes.Length >= pngSignature.Length &&
            bytes.AsSpan(0, pngSignature.Length).SequenceEqual(pngSignature))
        {
            return "image/png";
        }

        if (bytes.Length >= 3 &&
            bytes[0] == 255 &&
            bytes[1] == 216 &&
            bytes[2] == 255)
        {
            return "image/jpeg";
        }

        throw new ArgumentException(
            "Upload a PNG or JPEG image.",
            nameof(bytes));
    }
}
