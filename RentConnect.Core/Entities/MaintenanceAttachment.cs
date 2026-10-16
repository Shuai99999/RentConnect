namespace RentConnect.Core.Entities;

public sealed class MaintenanceAttachment
{
    public const int MaxOriginalFileNameLength = 255;
    public const int MaxContentTypeLength = 100;
    public const int MaxStorageKeyLength = 500;
    public const long MaxFileSizeBytes =
        10 * 1024 * 1024;

    private MaintenanceAttachment()
    {
    }

    public MaintenanceAttachment(
        Guid id,
        Guid maintenanceRequestId,
        Guid uploadedByUserProfileId,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        string storageKey,
        DateTimeOffset uploadedAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Attachment ID cannot be empty.",
                nameof(id));
        }

        if (maintenanceRequestId == Guid.Empty)
        {
            throw new ArgumentException(
                "Maintenance request ID cannot be empty.",
                nameof(maintenanceRequestId));
        }

        if (uploadedByUserProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "Uploader user profile ID cannot be empty.",
                nameof(uploadedByUserProfileId));
        }

        Id = id;
        MaintenanceRequestId = maintenanceRequestId;
        UploadedByUserProfileId =
            uploadedByUserProfileId;

        OriginalFileName =
            NormalizeFileName(originalFileName);

        ContentType =
            NormalizeContentType(contentType);

        FileSizeBytes =
            ValidateFileSize(fileSizeBytes);

        StorageKey =
            NormalizeStorageKey(storageKey);

        UploadedAtUtc =
            RequireUtc(
                uploadedAtUtc,
                nameof(uploadedAtUtc));
    }

    public Guid Id { get; private set; }

    public Guid MaintenanceRequestId
    {
        get;
        private set;
    }

    public Guid UploadedByUserProfileId
    {
        get;
        private set;
    }

    public string OriginalFileName
    {
        get;
        private set;
    } = string.Empty;

    public string ContentType
    {
        get;
        private set;
    } = string.Empty;

    public long FileSizeBytes { get; private set; }

    public string StorageKey
    {
        get;
        private set;
    } = string.Empty;

    public DateTimeOffset UploadedAtUtc
    {
        get;
        private set;
    }

    private static string NormalizeFileName(
        string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(
            originalFileName))
        {
            throw new ArgumentException(
                "Original file name is required.",
                nameof(originalFileName));
        }

        var normalizedPath =
            originalFileName
                .Trim()
                .Replace('\\', '/');

        var lastSeparatorIndex =
            normalizedPath.LastIndexOf('/');

        var normalizedFileName =
            lastSeparatorIndex >= 0
                ? normalizedPath[
                    (lastSeparatorIndex + 1)..]
                : normalizedPath;

        if (string.IsNullOrWhiteSpace(
                normalizedFileName) ||
            normalizedFileName is "." or "..")
        {
            throw new ArgumentException(
                "Original file name is invalid.",
                nameof(originalFileName));
        }

        if (normalizedFileName.Length >
            MaxOriginalFileNameLength)
        {
            throw new ArgumentException(
                $"Original file name cannot exceed {MaxOriginalFileNameLength} characters.",
                nameof(originalFileName));
        }

        return normalizedFileName;
    }

    private static string NormalizeContentType(
        string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException(
                "Content type is required.",
                nameof(contentType));
        }

        var normalizedContentType =
            contentType
                .Trim()
                .ToLowerInvariant();

        if (normalizedContentType.Length >
            MaxContentTypeLength)
        {
            throw new ArgumentException(
                $"Content type cannot exceed {MaxContentTypeLength} characters.",
                nameof(contentType));
        }

        if (normalizedContentType is not
            ("image/jpeg" or
             "image/png" or
             "image/webp"))
        {
            throw new ArgumentException(
                "Only JPEG, PNG, and WebP images are supported.",
                nameof(contentType));
        }

        return normalizedContentType;
    }

    private static long ValidateFileSize(
        long fileSizeBytes)
    {
        if (fileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fileSizeBytes),
                "File size must be greater than zero.");
        }

        if (fileSizeBytes > MaxFileSizeBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fileSizeBytes),
                $"File size cannot exceed {MaxFileSizeBytes} bytes.");
        }

        return fileSizeBytes;
    }

    private static string NormalizeStorageKey(
        string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException(
                "Storage key is required.",
                nameof(storageKey));
        }

        var normalizedStorageKey =
            storageKey.Trim();

        if (normalizedStorageKey.Length >
            MaxStorageKeyLength)
        {
            throw new ArgumentException(
                $"Storage key cannot exceed {MaxStorageKeyLength} characters.",
                nameof(storageKey));
        }

        return normalizedStorageKey;
    }

    private static DateTimeOffset RequireUtc(
        DateTimeOffset timestamp,
        string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The timestamp must use UTC.",
                parameterName);
        }

        return timestamp;
    }
}