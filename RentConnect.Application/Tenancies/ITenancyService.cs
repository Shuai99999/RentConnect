namespace RentConnect.Application.Tenancies;

public interface ITenancyService
{
    Task<IReadOnlyList<TenancySummary>> GetVisibleAsync(Guid userProfileId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenancyUnitOption>> GetAvailableUnitsAsync(Guid userProfileId, CancellationToken cancellationToken = default);
    Task<TenancyDetails?> GetDetailsAsync(Guid tenancyId, Guid userProfileId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateTenancyCommand command, Guid userProfileId, CancellationToken cancellationToken = default);
    Task UpdateEndDateAsync(UpdateTenancyEndDateCommand command, Guid userProfileId, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid tenancyId, Guid userProfileId, CancellationToken cancellationToken = default);
    Task<CreatedTenancyInvitation> InviteAsync(InviteTenancyMemberCommand command, Guid userProfileId, CancellationToken cancellationToken = default);
    Task RevokeInvitationAsync(Guid invitationId, Guid userProfileId, CancellationToken cancellationToken = default);
    Task<TenancyInvitationPreview?> GetInvitationAsync(string token, CancellationToken cancellationToken = default);
    Task<Guid> AcceptInvitationAsync(string token, Guid userProfileId, string email, CancellationToken cancellationToken = default);
    Task MarkMemberLeftAsync(MarkTenancyMemberLeftCommand command, Guid userProfileId, CancellationToken cancellationToken = default);
}
