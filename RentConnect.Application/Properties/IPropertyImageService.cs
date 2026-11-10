namespace RentConnect.Application.Properties;

public sealed record PropertyImageItem(
    Guid Id);

public sealed record PropertyImageContent(
    byte[] Content,
    string ContentType);

public interface IPropertyImageService
{
    Task<IReadOnlyList<PropertyImageItem>> GetAsync(
        Guid propertyId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Guid> AddAsync(
        Guid propertyId,
        Guid userId,
        Stream content,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid imageId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PropertyImageContent?> OpenAsync(
        Guid imageId,
        Guid userId,
        CancellationToken cancellationToken = default);
}