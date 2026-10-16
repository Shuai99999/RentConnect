namespace RentConnect.Application.Maintenance;

public interface IMaintenanceAttachmentStorage
{
    Task SaveAsync(
        string storageKey,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}