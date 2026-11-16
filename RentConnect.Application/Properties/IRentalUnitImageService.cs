namespace RentConnect.Application.Properties;

public sealed record RentalUnitImageItem(Guid Id);

public sealed record RentalUnitImageContent(
    byte[] Content,
    string ContentType);

public interface IRentalUnitImageService
{
    Task<IReadOnlyList<RentalUnitImageItem>> GetAsync(
        Guid rentalUnitId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Guid> AddAsync(
        Guid rentalUnitId,
        Guid userId,
        Stream content,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid imageId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<RentalUnitImageContent?> OpenAsync(
        Guid imageId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
