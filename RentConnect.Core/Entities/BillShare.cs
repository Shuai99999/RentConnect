using RentConnect.Core.ValueObjects;

namespace RentConnect.Core.Entities;

public sealed class BillShare
{
    private BillShare()
    {
    }

    internal BillShare(
        Guid id,
        Guid sharedBillId,
        Guid userProfileId,
        Money amount)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Bill share ID cannot be empty.",
                nameof(id));
        }

        if (sharedBillId == Guid.Empty)
        {
            throw new ArgumentException(
                "Shared bill ID cannot be empty.",
                nameof(sharedBillId));
        }

        if (userProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "User profile ID cannot be empty.",
                nameof(userProfileId));
        }

        ArgumentNullException.ThrowIfNull(amount);

        Id = id;
        SharedBillId = sharedBillId;
        UserProfileId = userProfileId;
        Amount = amount;
    }

    public Guid Id { get; private set; }

    public Guid SharedBillId { get; private set; }

    public Guid UserProfileId { get; private set; }

    public Money Amount { get; private set; } = null!;

    public DateTimeOffset? PaymentDeclaredAtUtc { get; private set; }

    public DateTimeOffset? PaymentConfirmedAtUtc { get; private set; }

    public bool IsPaymentDeclared => PaymentDeclaredAtUtc.HasValue;

    public bool IsSettled => PaymentConfirmedAtUtc.HasValue;

    internal void DeclarePayment(DateTimeOffset declaredAtUtc)
    {
        if (PaymentDeclaredAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Payment has already been declared for this bill share.");
        }

        PaymentDeclaredAtUtc =
            RequireUtc(declaredAtUtc, nameof(declaredAtUtc));
    }

    internal void ConfirmPayment(DateTimeOffset confirmedAtUtc)
    {
        if (!PaymentDeclaredAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Payment must be declared before it can be confirmed.");
        }

        if (PaymentConfirmedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Payment has already been confirmed for this bill share.");
        }

        var normalizedConfirmedAtUtc =
            RequireUtc(confirmedAtUtc, nameof(confirmedAtUtc));

        if (normalizedConfirmedAtUtc < PaymentDeclaredAtUtc.Value)
        {
            throw new ArgumentException(
                "Payment confirmation cannot be before its declaration.",
                nameof(confirmedAtUtc));
        }

        PaymentConfirmedAtUtc = normalizedConfirmedAtUtc;
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
