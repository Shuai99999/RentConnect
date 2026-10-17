using RentConnect.Core.Billing;
using RentConnect.Core.Enums;
using RentConnect.Core.ValueObjects;

namespace RentConnect.Core.Entities;

public sealed class SharedBill
{
    public const int MaxTitleLength = 150;

    private readonly List<BillShare> _shares = [];
    private readonly List<BillDispute> _disputes = [];

    private SharedBill()
    {
    }

    public SharedBill(
        Guid id,
        Guid tenancyId,
        Guid createdByUserProfileId,
        Guid paidByUserProfileId,
        string title,
        SharedBillCategory category,
        Money total,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateOnly dueDate,
        DateTimeOffset createdAtUtc)
    {
        RequireId(id, nameof(id), "Shared bill ID");
        RequireId(tenancyId, nameof(tenancyId), "Tenancy ID");
        RequireId(
            createdByUserProfileId,
            nameof(createdByUserProfileId),
            "Creator user profile ID");
        RequireId(
            paidByUserProfileId,
            nameof(paidByUserProfileId),
            "Paying user profile ID");

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "The bill category is invalid.");
        }

        ArgumentNullException.ThrowIfNull(total);

        if (total.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(total),
                "Bill total must be greater than zero.");
        }

        if (periodEnd < periodStart)
        {
            throw new ArgumentException(
                "Billing period end cannot be before its start.",
                nameof(periodEnd));
        }

        if (dueDate < periodEnd)
        {
            throw new ArgumentException(
                "Bill due date cannot be before the billing period ends.",
                nameof(dueDate));
        }

        Id = id;
        TenancyId = tenancyId;
        CreatedByUserProfileId = createdByUserProfileId;
        PaidByUserProfileId = paidByUserProfileId;
        Title = NormalizeTitle(title);
        Category = category;
        Total = total;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        DueDate = dueDate;
        SplitMethod = BillSplitMethod.Equal;
        Status = SharedBillStatus.Draft;
        CreatedAtUtc = RequireUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TenancyId { get; private set; }

    public Guid CreatedByUserProfileId { get; private set; }

    public Guid PaidByUserProfileId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public SharedBillCategory Category { get; private set; }

    public Money Total { get; private set; } = null!;

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public DateOnly DueDate { get; private set; }

    public BillSplitMethod SplitMethod { get; private set; }

    public SharedBillStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<BillShare> Shares => _shares;

    public IReadOnlyCollection<BillDispute> Disputes => _disputes;

    public void AllocateEqually(
        IEnumerable<(Guid ShareId, Guid UserProfileId)> participants,
        int decimalPlaces,
        DateTimeOffset changedAtUtc)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(participants);

        var participantArray = participants.ToArray();

        if (participantArray.Any(
            participant => participant.ShareId == Guid.Empty))
        {
            throw new ArgumentException(
                "Bill share IDs cannot be empty.",
                nameof(participants));
        }

        if (participantArray
                .Select(participant => participant.ShareId)
                .Distinct()
                .Count() != participantArray.Length)
        {
            throw new ArgumentException(
                "Bill share IDs must be unique.",
                nameof(participants));
        }

        var allocations = EqualBillSplitter.Split(
            Total,
            participantArray.Select(
                participant => participant.UserProfileId),
            decimalPlaces);

        var shareIdsByUserProfileId =
            participantArray.ToDictionary(
                participant => participant.UserProfileId,
                participant => participant.ShareId);

        var normalizedChangedAtUtc =
            RequireValidChangeTime(changedAtUtc);

        _shares.Clear();

        _shares.AddRange(
            allocations.Select(
                allocation => new BillShare(
                    shareIdsByUserProfileId[
                        allocation.UserProfileId],
                    Id,
                    allocation.UserProfileId,
                    allocation.Amount)));

        SplitMethod = BillSplitMethod.Equal;
        UpdatedAtUtc = normalizedChangedAtUtc;
    }

    public void Publish(DateTimeOffset publishedAtUtc)
    {
        EnsureDraft();

        if (_shares.Count == 0)
        {
            throw new InvalidOperationException(
                "A shared bill must have at least one share before publishing.");
        }

        ValidateShareTotal();

        var normalizedPublishedAtUtc =
            RequireValidChangeTime(publishedAtUtc);

        Status = SharedBillStatus.Published;
        UpdatedAtUtc = normalizedPublishedAtUtc;
    }

    public void Void(DateTimeOffset voidedAtUtc)
    {
        if (Status is not SharedBillStatus.Draft and
            not SharedBillStatus.Published)
        {
            throw new InvalidOperationException(
                $"A shared bill cannot be voided while its status is {Status}.");
        }

        var normalizedVoidedAtUtc =
            RequireValidChangeTime(voidedAtUtc);

        Status = SharedBillStatus.Void;
        UpdatedAtUtc = normalizedVoidedAtUtc;
    }

    public void DeclareSharePayment(
        Guid userProfileId,
        DateTimeOffset declaredAtUtc)
    {
        EnsurePaymentActivityAllowed();

        var share = GetShare(userProfileId);
        var normalizedDeclaredAtUtc =
            RequireValidChangeTime(declaredAtUtc);

        share.DeclarePayment(normalizedDeclaredAtUtc);
        UpdatedAtUtc = normalizedDeclaredAtUtc;
    }

    public void ConfirmSharePayment(
        Guid userProfileId,
        DateTimeOffset confirmedAtUtc)
    {
        EnsurePaymentActivityAllowed();

        var share = GetShare(userProfileId);
        var normalizedConfirmedAtUtc =
            RequireValidChangeTime(confirmedAtUtc);

        share.ConfirmPayment(normalizedConfirmedAtUtc);
        UpdatedAtUtc = normalizedConfirmedAtUtc;

        if (Status != SharedBillStatus.Disputed)
        {
            RefreshSettlementStatus();
        }
    }

    public void RaiseDispute(
        Guid disputeId,
        Guid raisedByUserProfileId,
        string reason,
        DateTimeOffset raisedAtUtc)
    {
        if (Status is not SharedBillStatus.Published and
            not SharedBillStatus.PartiallySettled)
        {
            throw new InvalidOperationException(
                $"A dispute cannot be raised while the bill status is {Status}.");
        }

        if (_disputes.Any(dispute => dispute.Id == disputeId))
        {
            throw new InvalidOperationException(
                "A bill dispute with this ID already exists.");
        }

        var normalizedRaisedAtUtc =
            RequireValidChangeTime(raisedAtUtc);

        var dispute = new BillDispute(
            disputeId,
            Id,
            raisedByUserProfileId,
            reason,
            normalizedRaisedAtUtc);

        _disputes.Add(dispute);
        Status = SharedBillStatus.Disputed;
        UpdatedAtUtc = normalizedRaisedAtUtc;
    }

    public void ResolveDispute(
        Guid disputeId,
        Guid resolvedByUserProfileId,
        string resolution,
        DateTimeOffset resolvedAtUtc)
    {
        if (Status != SharedBillStatus.Disputed)
        {
            throw new InvalidOperationException(
                "The shared bill does not have an active dispute.");
        }

        var dispute = _disputes.SingleOrDefault(
            dispute =>
                dispute.Id == disputeId &&
                !dispute.IsResolved)
            ?? throw new KeyNotFoundException(
                "The active bill dispute was not found.");

        var normalizedResolvedAtUtc =
            RequireValidChangeTime(resolvedAtUtc);

        dispute.Resolve(
            resolvedByUserProfileId,
            resolution,
            normalizedResolvedAtUtc);

        UpdatedAtUtc = normalizedResolvedAtUtc;
        RefreshSettlementStatus();
    }

    private BillShare GetShare(Guid userProfileId)
    {
        if (userProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "User profile ID cannot be empty.",
                nameof(userProfileId));
        }

        return _shares.SingleOrDefault(
            share => share.UserProfileId == userProfileId)
            ?? throw new KeyNotFoundException(
                "The bill share was not found.");
    }

    private void EnsurePaymentActivityAllowed()
    {
        if (Status is not SharedBillStatus.Published and
            not SharedBillStatus.PartiallySettled and
            not SharedBillStatus.Disputed)
        {
            throw new InvalidOperationException(
                $"Payment activity is not allowed while the bill status is {Status}.");
        }
    }

    private void RefreshSettlementStatus()
    {
        var settledShareCount =
            _shares.Count(share => share.IsSettled);

        Status = settledShareCount switch
        {
            0 => SharedBillStatus.Published,
            _ when settledShareCount == _shares.Count =>
                SharedBillStatus.Settled,
            _ => SharedBillStatus.PartiallySettled
        };
    }

    private void ValidateShareTotal()
    {
        if (_shares.Any(
            share => share.Amount.Currency != Total.Currency))
        {
            throw new InvalidOperationException(
                "Every bill share must use the bill currency.");
        }

        if (_shares.Sum(share => share.Amount.Amount) != Total.Amount)
        {
            throw new InvalidOperationException(
                "Bill shares must add up to the bill total.");
        }
    }

    private void EnsureDraft()
    {
        if (Status != SharedBillStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only a draft shared bill can be changed.");
        }
    }

    private DateTimeOffset RequireValidChangeTime(
        DateTimeOffset changedAtUtc)
    {
        var normalizedTime =
            RequireUtc(changedAtUtc, nameof(changedAtUtc));

        if (normalizedTime < UpdatedAtUtc)
        {
            throw new ArgumentException(
                "The change time cannot be before the previous update.",
                nameof(changedAtUtc));
        }

        return normalizedTime;
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException(
                "Bill title is required.",
                nameof(title));
        }

        var normalizedTitle = title.Trim();

        if (normalizedTitle.Length > MaxTitleLength)
        {
            throw new ArgumentException(
                $"Bill title cannot exceed {MaxTitleLength} characters.",
                nameof(title));
        }

        return normalizedTitle;
    }

    private static void RequireId(
        Guid value,
        string parameterName,
        string displayName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                $"{displayName} cannot be empty.",
                parameterName);
        }
    }

    private static DateTimeOffset RequireUtc(
        DateTimeOffset value,
        string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The timestamp must use UTC.",
                parameterName);
        }

        return value;
    }
}
