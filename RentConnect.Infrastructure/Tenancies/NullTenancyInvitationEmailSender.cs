using RentConnect.Application.Tenancies;

namespace RentConnect.Infrastructure.Tenancies;

public sealed class NullTenancyInvitationEmailSender : ITenancyInvitationEmailSender
{
    public static NullTenancyInvitationEmailSender Instance { get; } = new();

    public Task<bool> SendAsync(
        TenancyInvitationEmail message,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
