using System.Text;
using RentConnect.Infrastructure.Maintenance;

namespace RentConnect.IntegrationTests;

public sealed class LocalMaintenanceAttachmentStorageTests
    : IDisposable
{
    private readonly string _rootPath;
    private readonly LocalMaintenanceAttachmentStorage
        _storage;

    public LocalMaintenanceAttachmentStorageTests()
    {
        _rootPath =
            Path.Combine(
                Path.GetTempPath(),
                "RentConnectTests",
                Guid.NewGuid().ToString("N"));

        _storage =
            new LocalMaintenanceAttachmentStorage(
                _rootPath);
    }

    [Fact]
    public async Task SaveAndOpenReadAsync_RoundTripsContent()
    {
        const string storageKey =
            "maintenance/request/photo.jpg";

        var expectedBytes =
            Encoding.UTF8.GetBytes(
                "test image content");

        await using var source =
            new MemoryStream(expectedBytes);

        await _storage.SaveAsync(
            storageKey,
            source);

        await using var stored =
            await _storage.OpenReadAsync(
                storageKey);

        Assert.NotNull(stored);

        using var result =
            new MemoryStream();

        await stored.CopyToAsync(result);

        Assert.Equal(
            expectedBytes,
            result.ToArray());
    }

    [Fact]
    public async Task DeleteAsync_RemovesStoredFile()
    {
        const string storageKey =
            "maintenance/request/photo.png";

        await using var source =
            new MemoryStream([1, 2, 3]);

        await _storage.SaveAsync(
            storageKey,
            source);

        await _storage.DeleteAsync(storageKey);

        await using var stored =
            await _storage.OpenReadAsync(
                storageKey);

        Assert.Null(stored);
    }

    [Fact]
    public async Task OpenReadAsync_WhenFileDoesNotExist_ReturnsNull()
    {
        await using var stored =
            await _storage.OpenReadAsync(
                "maintenance/missing/photo.webp");

        Assert.Null(stored);
    }

    [Fact]
    public async Task SaveAsync_WhenFileExists_DoesNotOverwriteOrDeleteIt()
    {
        const string storageKey =
            "maintenance/request/photo.jpg";

        await using var original =
            new MemoryStream([1, 2, 3]);

        await _storage.SaveAsync(
            storageKey,
            original);

        await using var replacement =
            new MemoryStream([9, 9, 9]);

        await Assert.ThrowsAsync<IOException>(
            () => _storage.SaveAsync(
                storageKey,
                replacement));

        await using var stored =
            await _storage.OpenReadAsync(
                storageKey);

        Assert.NotNull(stored);

        using var result =
            new MemoryStream();

        await stored.CopyToAsync(result);

        Assert.Equal(
            new byte[] { 1, 2, 3 },
            result.ToArray());
    }

    [Fact]
    public async Task SaveAsync_WithPathTraversal_Throws()
    {
        await using var source =
            new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _storage.SaveAsync(
                "../outside.jpg",
                source));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(
                _rootPath,
                recursive: true);
        }
    }
}