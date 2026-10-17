using RentConnect.Core.ValueObjects;

namespace RentConnect.Core.Billing;

public static class EqualBillSplitter
{
    public static IReadOnlyList<BillAllocation> Split(
        Money total,
        IEnumerable<Guid> participantIds,
        int decimalPlaces = 2)
    {
        ArgumentNullException.ThrowIfNull(total);
        ArgumentNullException.ThrowIfNull(participantIds);

        if (total.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(total),
                "Bill total must be greater than zero.");
        }

        if (decimalPlaces is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decimalPlaces),
                "Decimal places must be between zero and four.");
        }

        var participants = participantIds
            .OrderBy(participantId => participantId)
            .ToArray();

        if (participants.Length == 0)
        {
            throw new ArgumentException(
                "At least one participant is required.",
                nameof(participantIds));
        }

        if (participants.Any(participantId => participantId == Guid.Empty))
        {
            throw new ArgumentException(
                "Participant identifiers cannot be empty.",
                nameof(participantIds));
        }

        if (participants.Distinct().Count() != participants.Length)
        {
            throw new ArgumentException(
                "Participant identifiers must be unique.",
                nameof(participantIds));
        }

        var smallestUnit = GetSmallestUnit(decimalPlaces);

        if (total.Amount !=
            decimal.Round(
                total.Amount,
                decimalPlaces,
                MidpointRounding.ToZero))
        {
            throw new ArgumentException(
                "Bill total has more decimal places than the currency supports.",
                nameof(total));
        }

        var baseAmount =
            decimal.Floor(
                total.Amount /
                participants.Length /
                smallestUnit) *
            smallestUnit;

        var remainderUnits =
            decimal.ToInt32(
                (total.Amount -
                    baseAmount * participants.Length) /
                smallestUnit);

        return participants
            .Select(
                (participantId, index) =>
                    new BillAllocation(
                        participantId,
                        new Money(
                            baseAmount +
                                (index < remainderUnits
                                    ? smallestUnit
                                    : 0m),
                            total.Currency)))
            .ToArray();
    }

    private static decimal GetSmallestUnit(int decimalPlaces)
    {
        var factor = 1m;

        for (var index = 0; index < decimalPlaces; index++)
        {
            factor *= 10m;
        }

        return 1m / factor;
    }
}
