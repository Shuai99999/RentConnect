using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using RentConnect.Application.Maintenance;

namespace RentConnect.Infrastructure.Maintenance;

public sealed class AzureBlobMaintenanceAttachmentStorage
    : IMaintenanceAttachmentStorage
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobMaintenanceAttachmentStorage(
        Uri blobServiceUri,
        string containerName)
    {
        ArgumentNullException.ThrowIfNull(blobServiceUri);

        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new ArgumentException(
                "A Blob Storage container name is required.",
                nameof(containerName));
        }

        var serviceClient =
            new BlobServiceClient(
                blobServiceUri,
                new DefaultAzureCredential());

        _containerClient =
            serviceClient.GetBlobContainerClient(
                containerName.Trim());
    }

    public async Task SaveAsync(
        string storageKey,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanRead)
        {
            throw new ArgumentException(
                "The content stream must be readable.",
                nameof(content));
        }

        var blobClient =
            _containerClient.GetBlobClient(
                NormalizeStorageKey(storageKey));

        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                Conditions =
                    new BlobRequestConditions
                    {
                        IfNoneMatch = ETag.All,
                    },
            },
            cancellationToken);
    }

    public async Task<Stream?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var blobClient =
            _containerClient.GetBlobClient(
                NormalizeStorageKey(storageKey));

        try
        {
            var response =
                await blobClient.DownloadStreamingAsync(
                    cancellationToken: cancellationToken);

            return response.Value.Content;
        }
        catch (RequestFailedException exception)
            when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var blobClient =
            _containerClient.GetBlobClient(
                NormalizeStorageKey(storageKey));

        await blobClient.DeleteIfExistsAsync(
            DeleteSnapshotsOption.IncludeSnapshots,
            cancellationToken: cancellationToken);
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

        var normalizedKey =
            storageKey
                .Trim()
                .Replace('\\', '/');

        if (normalizedKey.StartsWith('/'))
        {
            throw new ArgumentException(
                "Storage key must be relative.",
                nameof(storageKey));
        }

        var segments =
            normalizedKey.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 ||
            segments.Any(
                segment =>
                    segment is "." or ".."))
        {
            throw new ArgumentException(
                "Storage key contains an invalid path segment.",
                nameof(storageKey));
        }

        return string.Join('/', segments);
    }
}
