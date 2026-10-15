namespace RentConnect.Core.ValueObjects;

public sealed record Address
{
    private Address()
    {
    }

    public Address(
        string line1,
        string? line2,
        string city,
        string? region,
        string? postalCode,
        string countryCode)
    {
        Line1 = RequireValue(line1, nameof(line1));
        Line2 = NormalizeOptionalValue(line2);
        City = RequireValue(city, nameof(city));
        Region = NormalizeOptionalValue(region);
        PostalCode = NormalizeOptionalValue(postalCode);

        var normalizedCountryCode =
            RequireValue(countryCode, nameof(countryCode))
                .ToUpperInvariant();

        if (normalizedCountryCode.Length != 2 ||
            normalizedCountryCode.Any(
                character => !char.IsAsciiLetter(character)))
        {
            throw new ArgumentException(
                "Country code must be a two-letter ISO country code.",
                nameof(countryCode));
        }

        CountryCode = normalizedCountryCode;
    }

    public string Line1 { get; private set; } = string.Empty;

    public string? Line2 { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string? Region { get; private set; }

    public string? PostalCode { get; private set; }

    public string CountryCode { get; private set; } = string.Empty;

    private static string RequireValue(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Value is required.",
                parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}