namespace RentConnect.Application.Maintenance;

public interface IMaintenanceService
{
    Task<IReadOnlyList<MaintenanceTenancyOption>>
        GetAvailableTenanciesAsync(
            Guid userProfileId,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MaintenanceRequestSummary>>
        GetVisibleRequestsAsync(
            Guid userProfileId,
            CancellationToken cancellationToken = default);

    Task<MaintenanceRequestDetails?> GetDetailsAsync(
        Guid maintenanceRequestId,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(
        CreateMaintenanceRequestCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task UpdateDetailsAsync(
        UpdateMaintenanceRequestCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task<Guid> AddMessageAsync(
        AddMaintenanceMessageCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task EditMessageAsync(
        EditMaintenanceMessageCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task DeleteMessageAsync(
        DeleteMaintenanceMessageCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task<Guid> AddAttachmentAsync(
        AddMaintenanceAttachmentCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task<MaintenanceAttachmentDownload?> OpenAttachmentAsync(
        Guid attachmentId,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task DeleteAttachmentAsync(
        DeleteMaintenanceAttachmentCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);

    Task ChangeStatusAsync(
        ChangeMaintenanceStatusCommand command,
        Guid userProfileId,
        CancellationToken cancellationToken = default);
}
