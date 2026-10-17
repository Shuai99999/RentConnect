namespace RentConnect.Core.Policies;

public sealed record SharedBillAccessContext(
    Guid CurrentUserProfileId,
    bool IsPropertyOwner,
    bool IsActiveTenancyMember,
    bool HasBillShare);
