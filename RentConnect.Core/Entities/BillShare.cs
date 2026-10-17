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
}
