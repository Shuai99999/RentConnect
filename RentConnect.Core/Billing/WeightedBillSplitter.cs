using RentConnect.Core.ValueObjects;

namespace RentConnect.Core.Billing;

public static class WeightedBillSplitter
{
    public static IReadOnlyList<BillAllocation> Split(
        Money total,
        IEnumerable<(Guid UserProfileId, decimal Weight)> participants,
        int decimalPlaces = 2)
    {
        ArgumentNullException.ThrowIfNull(total);
        ArgumentNullException.ThrowIfNull(participants);

        if (total.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(total));
        }

        if (decimalPlaces is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(decimalPlaces));
        }

        var items = participants.OrderBy(item => item.UserProfileId).ToArray();
        if (items.Length == 0 ||
            items.Any(item => item.UserProfileId == Guid.Empty || item.Weight <= 0) ||
            items.Select(item => item.UserProfileId).Distinct().Count() != items.Length)
        {
            throw new ArgumentException(
                "Participants must be unique and have positive weights.",
                nameof(participants));
        }

        var scale = Pow10(decimalPlaces);
        var totalUnitsDecimal = total.Amount * scale;
        if (totalUnitsDecimal != decimal.Truncate(totalUnitsDecimal))
        {
            throw new ArgumentException(
                "Bill total has more decimal places than the currency supports.",
                nameof(total));
        }

        var totalUnits = decimal.ToInt64(totalUnitsDecimal);
        var totalWeight = items.Sum(item => item.Weight);
        var calculated = items
            .Select(item =>
            {
                var exactUnits = totalUnits * item.Weight / totalWeight;
                var baseUnits = decimal.ToInt64(decimal.Floor(exactUnits));
                return new
                {
                    item.UserProfileId,
                    BaseUnits = baseUnits,
                    Remainder = exactUnits - baseUnits
                };
            })
            .ToArray();

        var unitsToDistribute = totalUnits - calculated.Sum(item => item.BaseUnits);
        var bonusRecipients = calculated
            .OrderByDescending(item => item.Remainder)
            .ThenBy(item => item.UserProfileId)
            .Take(checked((int)unitsToDistribute))
            .Select(item => item.UserProfileId)
            .ToHashSet();

        return calculated
            .OrderBy(item => item.UserProfileId)
            .Select(item => new BillAllocation(
                item.UserProfileId,
                new Money(
                    (item.BaseUnits + (bonusRecipients.Contains(item.UserProfileId) ? 1 : 0)) / scale,
                    total.Currency)))
            .ToArray();
    }

    private static decimal Pow10(int decimalPlaces)
    {
        var result = 1m;
        for (var index = 0; index < decimalPlaces; index++)
        {
            result *= 10m;
        }

        return result;
    }
}
