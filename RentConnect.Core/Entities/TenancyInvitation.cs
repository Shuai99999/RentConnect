namespace RentConnect.Core.Entities;

public sealed class TenancyInvitation
{
    private TenancyInvitation()
    {
    }

    public TenancyInvitation(
        Guid id,
        Guid tenancyId,
        string email,
        string tokenHash,
        Guid invitedByUserProfileId,
        DateOnly joinedOn,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Invitation ID cannot be empty.", nameof(id));
        }

        if (tenancyId == Guid.Empty)
        {
            throw new ArgumentException("Tenancy ID cannot be empty.", nameof(tenancyId));
        }

        if (invitedByUserProfileId == Guid.Empty)
        {
            throw new ArgumentException("Inviter ID cannot be empty.", nameof(invitedByUserProfileId));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        if (expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentException("Invitation expiry must be after creation.", nameof(expiresAtUtc));
        }

        Id = id;
        TenancyId = tenancyId;
        Email = email.Trim();
        NormalizedEmail = NormalizeEmail(email);
        TokenHash = tokenHash;
        InvitedByUserProfileId = invitedByUserProfileId;
        JoinedOn = joinedOn;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TenancyId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public Guid InvitedByUserProfileId { get; private set; }
    public DateOnly JoinedOn { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public bool CanBeAcceptedAt(DateTimeOffset now) =>
        AcceptedAtUtc is null && RevokedAtUtc is null && now < ExpiresAtUtc;

    public void Accept(DateTimeOffset acceptedAtUtc)
    {
        if (!CanBeAcceptedAt(acceptedAtUtc))
        {
            throw new InvalidOperationException("The invitation is no longer available.");
        }

        AcceptedAtUtc = acceptedAtUtc;
    }

    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        if (AcceptedAtUtc.HasValue)
        {
            throw new InvalidOperationException("An accepted invitation cannot be revoked.");
        }

        if (RevokedAtUtc.HasValue)
        {
            throw new InvalidOperationException("The invitation has already been revoked.");
        }

        RevokedAtUtc = revokedAtUtc;
    }

    public static string NormalizeEmail(string email) =>
        email.Trim().ToUpperInvariant();
}
