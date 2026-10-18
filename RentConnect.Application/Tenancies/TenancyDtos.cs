namespace RentConnect.Application.Tenancies;

public sealed record TenancySummary(Guid Id, string PropertyName, string RentalUnitName, DateOnly StartDate, DateOnly? EndDate, bool IsCancelled, int MemberCount, bool CanManage);
public sealed record TenancyUnitOption(Guid RentalUnitId, string PropertyName, string RentalUnitName);
public sealed record TenancyMemberItem(Guid UserProfileId, string DisplayName, string Email, DateOnly JoinedOn, DateOnly? LeftOn);
public sealed record TenancyInvitationItem(Guid Id, string Email, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc, DateTimeOffset? AcceptedAtUtc, DateTimeOffset? RevokedAtUtc);
public sealed record TenancyDetails(Guid Id, Guid PropertyId, string PropertyName, Guid RentalUnitId, string RentalUnitName, DateOnly StartDate, DateOnly? EndDate, bool IsCancelled, bool CanManage, IReadOnlyList<TenancyMemberItem> Members, IReadOnlyList<TenancyInvitationItem> Invitations);
public sealed record CreatedTenancyInvitation(Guid InvitationId, string Token, DateTimeOffset ExpiresAtUtc);
public sealed record TenancyInvitationPreview(string PropertyName, string RentalUnitName, string Email, DateTimeOffset ExpiresAtUtc, bool IsAvailable);
