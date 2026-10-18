using RentConnect.Core.Enums;

namespace RentConnect.Application.Billing;

public sealed record SharedBillTenancyOption(
    Guid TenancyId,
    string PropertyName,
    string RentalUnitName);

public sealed record SharedBillParticipantOption(
    Guid UserProfileId,
    string DisplayName);

public sealed record SharedBillSummary(
    Guid Id,
    Guid TenancyId,
    string Title,
    SharedBillCategory Category,
    decimal TotalAmount,
    string Currency,
    DateOnly DueDate,
    SharedBillStatus Status,
    DateTimeOffset UpdatedAtUtc);

public sealed record BillShareItem(
    Guid UserProfileId,
    string DisplayName,
    decimal Amount,
    string Currency,
    DateTimeOffset? PaymentDeclaredAtUtc,
    DateTimeOffset? PaymentConfirmedAtUtc);

public sealed record BillDisputeItem(
    Guid Id,
    Guid RaisedByUserProfileId,
    string RaisedByDisplayName,
    string Reason,
    DateTimeOffset RaisedAtUtc,
    Guid? ResolvedByUserProfileId,
    string? ResolvedByDisplayName,
    string? Resolution,
    DateTimeOffset? ResolvedAtUtc);

public sealed record BillAttachmentItem(
    Guid Id,
    Guid UploadedByUserProfileId,
    string UploadedByDisplayName,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    DateTimeOffset UploadedAtUtc,
    bool CanDelete);

public sealed record BillAttachmentDownload(
    string OriginalFileName,
    string ContentType,
    Stream Content);

public sealed record SharedBillPermissions(
    bool CanAddAttachment,
    bool CanPublish,
    bool CanVoid,
    bool CanDeclarePayment,
    bool CanConfirmPayment,
    bool CanRaiseDispute,
    bool CanResolveDispute);

public sealed record SharedBillDetails(
    Guid Id,
    Guid TenancyId,
    Guid CreatedByUserProfileId,
    string CreatedByDisplayName,
    Guid PaidByUserProfileId,
    string PaidByDisplayName,
    string Title,
    SharedBillCategory Category,
    decimal TotalAmount,
    string Currency,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    BillSplitMethod SplitMethod,
    SharedBillStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<BillShareItem> Shares,
    IReadOnlyList<BillDisputeItem> Disputes,
    IReadOnlyList<BillAttachmentItem> Attachments,
    SharedBillPermissions Permissions);
