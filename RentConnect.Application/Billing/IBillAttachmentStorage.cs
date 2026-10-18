namespace RentConnect.Application.Billing;

public interface IBillAttachmentStorage
{
    Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
