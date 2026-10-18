namespace RentConnect.Application.Tenancies;

public sealed record CreateTenancyCommand(Guid RentalUnitId, DateOnly StartDate, DateOnly? EndDate);
public sealed record UpdateTenancyEndDateCommand(Guid TenancyId, DateOnly? EndDate);
public sealed record InviteTenancyMemberCommand(Guid TenancyId, string Email, DateOnly JoinedOn);
public sealed record MarkTenancyMemberLeftCommand(Guid TenancyId, Guid MemberUserProfileId, DateOnly LeftOn);
