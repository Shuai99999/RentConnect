using RentConnect.Core.Enums;

namespace RentConnect.Application.Billing;

public sealed record CreateEqualSharedBillCommand(
    Guid TenancyId,
    Guid PaidByUserProfileId,
    string Title,
    SharedBillCategory Category,
    decimal TotalAmount,
    string Currency,
    int CurrencyDecimalPlaces,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate);
