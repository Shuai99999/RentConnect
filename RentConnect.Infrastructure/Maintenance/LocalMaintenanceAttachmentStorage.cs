using RentConnect.Application.Maintenance;

namespace RentConnect.Infrastructure.Maintenance;

public sealed class LocalMaintenanceAttachmentStorage
    : IMaintenanceAttachmentStorage,
      RentConnect.Application.Billing.IBillAttachmentStorage
{
    private readonly string _rootPath;
    private readonly string _rootPathWithSeparator;

    public LocalMaintenanceAttachmentStorage(
        string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException(
                "Storage root path is required.",
                nameof(rootPath));
        }

        _rootPath =
            Path.GetFullPath(rootPath);

        _rootPathWithSeparator =
            _rootPath.EndsWith(
                Path.DirectorySeparatorChar)
                ? _rootPath
                : _rootPath +
                  Path.DirectorySeparatorChar;
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

        var filePath =
            ResolveFilePath(storageKey);

        var directoryPath =
            Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException(
                "The attachment directory could not be resolved.");

        Directory.CreateDirectory(directoryPath);

        var fileWasCreated = false;

        try
        {
            await using var destination =
                new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    FileOptions.Asynchronous);

            fileWasCreated = true;

            await content.CopyToAsync(
                destination,
                cancellationToken);
        }
        catch
        {
            if (fileWasCreated &&
                File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var filePath =
            ResolveFilePath(storageKey);

        if (!File.Exists(filePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream =
            new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var filePath =
            ResolveFilePath(storageKey);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    private string ResolveFilePath(
        string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException(
                "Storage key is required.",
                nameof(storageKey));
        }

        var relativePath =
            storageKey
                .Trim()
                .Replace(
                    '/',
                    Path.DirectorySeparatorChar)
                .Replace(
                    '\\',
                    Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException(
                "Storage key must be relative.",
                nameof(storageKey));
        }

        var filePath =
            Path.GetFullPath(
                Path.Combine(
                    _rootPath,
                    relativePath));

        if (!filePath.StartsWith(
                _rootPathWithSeparator,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Storage key cannot leave the storage root.",
                nameof(storageKey));
        }

        return filePath;
    }
}
