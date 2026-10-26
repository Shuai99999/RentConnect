namespace RentConnect.Application.Tenancies;

public interface ITenancyInvitationEmailSender
{
    Task<bool> SendAsync(
        TenancyInvitationEmail message,
        CancellationToken cancellationToken = default);
}

public sealed record TenancyInvitationEmail(
    string RecipientEmail,
    string PropertyName,
    string RentalUnitName,
    string Token,
    DateTimeOffset ExpiresAtUtc);
