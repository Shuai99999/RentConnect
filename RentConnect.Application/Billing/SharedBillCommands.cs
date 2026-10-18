using RentConnect.Core.Enums;

namespace RentConnect.Application.Billing;

public sealed record AddBillAttachmentCommand(
    Guid SharedBillId,
    string OriginalFileName,
    string ContentType,
    Stream Content);

public sealed record DeleteBillAttachmentCommand(Guid AttachmentId);

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

public sealed record PublishSharedBillCommand(Guid SharedBillId);

public sealed record VoidSharedBillCommand(Guid SharedBillId);

public sealed record DeclareBillPaymentCommand(Guid SharedBillId);

public sealed record ConfirmBillPaymentCommand(
    Guid SharedBillId,
    Guid UserProfileId);

public sealed record RaiseBillDisputeCommand(
    Guid SharedBillId,
    string Reason);

public sealed record ResolveBillDisputeCommand(
    Guid SharedBillId,
    Guid DisputeId,
    string Resolution);
