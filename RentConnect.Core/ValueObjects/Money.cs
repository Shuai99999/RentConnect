namespace RentConnect.Core.ValueObjects;

public sealed record Money
{
    private Money()
    {
    }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Amount cannot be negative.");
        }

        var normalizedCurrency =
            currency?.Trim().ToUpperInvariant();

        if (normalizedCurrency is null ||
            normalizedCurrency.Length != 3 ||
            normalizedCurrency.Any(
                character => !char.IsAsciiLetter(character)))
        {
            throw new ArgumentException(
                "Currency must be a three-letter ISO currency code.",
                nameof(currency));
        }

        Amount = amount;
        Currency = normalizedCurrency;
    }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;
}
