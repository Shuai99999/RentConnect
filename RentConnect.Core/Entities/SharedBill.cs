using RentConnect.Core.Billing;
using RentConnect.Core.Enums;
using RentConnect.Core.ValueObjects;

namespace RentConnect.Core.Entities;

public sealed class SharedBill
{
    public const int MaxTitleLength = 150;

    private readonly List<BillShare> _shares = [];

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
