namespace RentConnect.Application.Listings;

public interface IRentalListingService
{
    Task<IReadOnlyList<PublicListingSummary>> SearchAsync(
        string? city,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PublicListingSummary>> GetPublishedAsync(
        CancellationToken cancellationToken = default);

    Task<PublicListingDetails?> GetDetailsAsync(
        Guid listingId,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OwnedListingUnit>> GetOwnedUnitsAsync(
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task<OwnedListingUnit?> GetOwnedUnitAsync(
        Guid rentalUnitId,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task<Guid> PublishAsync(
        PublishListingCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task<Guid> SaveAsync(
        PublishListingCommand command,
        bool publish,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task SetPublishedAsync(
        Guid listingId,
        bool isPublished,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task ApplyAsync(
        ApplyForListingCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManagedRentalApplication>>
        GetManagedApplicationsAsync(
            Guid userProfileId,
            CancellationToken cancellationToken = default);

    Task<Guid> ApproveAsync(
        Guid applicationId,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task RejectAsync(
        Guid applicationId,
        Guid userProfileId,
        CancellationToken cancellationToken = default);
}