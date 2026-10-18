namespace RentConnect.Core.Entities;

public sealed class BillAttachment
{
    public const int MaxOriginalFileNameLength = 255;
    public const int MaxContentTypeLength = 100;
    public const int MaxStorageKeyLength = 500;
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private BillAttachment()
    {
    }

    public BillAttachment(
        Guid id,
        Guid sharedBillId,
        Guid uploadedByUserProfileId,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        string storageKey,
        DateTimeOffset uploadedAtUtc)
    {
        RequireId(id, nameof(id));
        RequireId(sharedBillId, nameof(sharedBillId));
        RequireId(uploadedByUserProfileId, nameof(uploadedByUserProfileId));

        Id = id;
        SharedBillId = sharedBillId;
        UploadedByUserProfileId = uploadedByUserProfileId;
        OriginalFileName = NormalizeFileName(originalFileName);
        ContentType = NormalizeContentType(contentType);
        FileSizeBytes = ValidateFileSize(fileSizeBytes);
        StorageKey = NormalizeStorageKey(storageKey);
        UploadedAtUtc = RequireUtc(uploadedAtUtc);
    }

    public Guid Id { get; private set; }
    public Guid SharedBillId { get; private set; }
    public Guid UploadedByUserProfileId { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public DateTimeOffset UploadedAtUtc { get; private set; }

    private static string NormalizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Original file name is required.", nameof(value));
        }

        var normalizedPath = value.Trim().Replace('\\', '/');
        var fileName = normalizedPath[(normalizedPath.LastIndexOf('/') + 1)..];

        if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or "..")
        {
            throw new ArgumentException("Original file name is invalid.", nameof(value));
        }

        if (fileName.Length > MaxOriginalFileNameLength)
        {
            throw new ArgumentException(
                $"Original file name cannot exceed {MaxOriginalFileNameLength} characters.",
                nameof(value));
        }

        return fileName;
    }

    private static string NormalizeContentType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Content type is required.", nameof(value));
        }

        var contentType = value.Trim().ToLowerInvariant();

        if (contentType.Length > MaxContentTypeLength ||
            contentType is not ("image/jpeg" or "image/png" or "image/webp" or "application/pdf"))
        {
            throw new ArgumentException(
                "Only JPEG, PNG, WebP, and PDF files are supported.",
                nameof(value));
        }

        return contentType;
    }

    private static long ValidateFileSize(long value)
    {
        if (value <= 0 || value > MaxFileSizeBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"File size must be between 1 and {MaxFileSizeBytes} bytes.");
        }

        return value;
    }

    private static string NormalizeStorageKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > MaxStorageKeyLength)
        {
            throw new ArgumentException("Storage key is invalid.", nameof(value));
        }

        return value.Trim();
    }

    private static DateTimeOffset RequireUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The timestamp must use UTC.", nameof(value));
        }

        return value;
    }

    private static void RequireId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("ID cannot be empty.", parameterName);
        }
    }
}
