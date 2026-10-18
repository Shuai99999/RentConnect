namespace RentConnect.Application.Properties;

public interface IPropertyService
{
    Task<IReadOnlyList<PropertySummary>> GetVisiblePropertiesAsync(
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task<PropertyDetails?> GetDetailsAsync(
        Guid propertyId,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(
        CreatePropertyCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        UpdatePropertyCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task<Guid> AddRentalUnitAsync(
        AddRentalUnitCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task UpdateRentalUnitAsync(
        UpdateRentalUnitCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task SetRentalUnitActiveAsync(
        SetRentalUnitActiveCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);
}
