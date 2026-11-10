namespace RentConnect.Core.Entities;

public sealed class PropertyImage
{
    public const int MaxBytes = 5 * 1024 * 1024;

    private PropertyImage()
    {
    }

    public PropertyImage(
        Guid propertyId,
        byte[] content,
        DateTimeOffset uploadedAtUtc)
    {
        if (propertyId == Guid.Empty)
        {
            throw new ArgumentException(
                "Property is required.");
        }

        ArgumentNullException.ThrowIfNull(content);

        if (content.Length == 0 || content.Length > MaxBytes)
        {
            throw new ArgumentException(
                "Images must be between 1 byte and 5 MB.");
        }

        ContentType = DetectContentType(content);

        Id = Guid.NewGuid();
        PropertyId = propertyId;
        Content = content.ToArray();
        UploadedAtUtc = uploadedAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public Guid PropertyId { get; private set; }

    public string ContentType { get; private set; } = "";

    public byte[] Content { get; private set; } = [];

    public DateTimeOffset UploadedAtUtc { get; private set; }

    private static string DetectContentType(byte[] bytes)
    {
        var pngSignature = new byte[]
        {
            137, 80, 78, 71, 13, 10, 26, 10
        };

        if (bytes.Length >= 8 &&
            bytes.AsSpan(0, 8).SequenceEqual(pngSignature))
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
            "Upload a PNG or JPEG image.");
    }
}