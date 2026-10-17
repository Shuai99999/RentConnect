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

    Task PublishAsync(
        PublishSharedBillCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task VoidAsync(
        VoidSharedBillCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task DeclarePaymentAsync(
        DeclareBillPaymentCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task ConfirmPaymentAsync(
        ConfirmBillPaymentCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task RaiseDisputeAsync(
        RaiseBillDisputeCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task ResolveDisputeAsync(
        ResolveBillDisputeCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);
}
