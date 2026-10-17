using RentConnect.Core.ValueObjects;

namespace RentConnect.Core.Billing;

public sealed record BillAllocation(
    Guid UserProfileId,
    Money Amount);
