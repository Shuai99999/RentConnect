namespace RentConnect.Application.Billing;

public interface ISharedBillService
{
    Task<IReadOnlyList<SharedBillTenancyOption>>
        GetAvailableTenanciesAsync(
            Guid userProfileId,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SharedBillSummary>> GetVisibleBillsAsync(
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task<SharedBillDetails?> GetDetailsAsync(
        Guid sharedBillId,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateEqualSplitAsync(
        CreateEqualSharedBillCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);
}
