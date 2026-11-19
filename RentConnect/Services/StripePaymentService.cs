using Stripe;
using Stripe.Checkout;

namespace RentConnect.Services;

public class StripePaymentService
{
    private const string BillIdMetadataKey = "rentconnect_bill_id";
    private const string UserProfileIdMetadataKey = "rentconnect_user_profile_id";

    private readonly IConfiguration _configuration;

    public StripePaymentService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<string> CreateBillCheckoutSessionAsync(
        Guid billId,
        Guid userProfileId,
        decimal amount,
        string currency,
        string billTitle,
        string baseUrl)
    {
        if (billId == Guid.Empty)
        {
            throw new ArgumentException("Bill ID is required.", nameof(billId));
        }

        if (userProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "User profile ID is required.",
                nameof(userProfileId));
        }

        var normalizedBaseUrl = ValidateBaseUrl(baseUrl);
        var normalizedCurrency = NormalizeCurrency(currency);
        var amountInMinorUnits = ToMinorUnits(amount, normalizedCurrency);
        var reference = $"{billId:N}:{userProfileId:N}";

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = reference,
            Metadata = new Dictionary<string, string>
            {
                [BillIdMetadataKey] = billId.ToString("D"),
                [UserProfileIdMetadataKey] = userProfileId.ToString("D")
            },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = normalizedCurrency.ToLowerInvariant(),
                        UnitAmount = amountInMinorUnits,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = string.IsNullOrWhiteSpace(billTitle)
                                ? "RentConnect bill payment"
                                : billTitle.Trim()
                        }
                    },
                    Quantity = 1
                }
            ],
            SuccessUrl =
                $"{normalizedBaseUrl}/bills/{billId:D}/payment-success?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{normalizedBaseUrl}/bills/{billId:D}"
        };

        var service = new SessionService(new StripeClient(GetSecretKey()));
        var session = await service.CreateAsync(options);

        if (string.IsNullOrWhiteSpace(session.Url))
        {
            throw new InvalidOperationException(
                "Stripe Checkout URL was not returned.");
        }

        return session.Url;
    }

    public async Task<bool> VerifyBillPaymentAsync(
        string sessionId,
        Guid billId,
        Guid userProfileId,
        decimal expectedAmount,
        string expectedCurrency,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        var normalizedCurrency = NormalizeCurrency(expectedCurrency);
        var expectedMinorUnits = ToMinorUnits(expectedAmount, normalizedCurrency);
        var service = new SessionService(new StripeClient(GetSecretKey()));
        var session = await service.GetAsync(
            sessionId.Trim(),
            cancellationToken: cancellationToken);

        return session.PaymentStatus == "paid" &&
               session.AmountTotal == expectedMinorUnits &&
               string.Equals(
                   session.Currency,
                   normalizedCurrency,
                   StringComparison.OrdinalIgnoreCase) &&
               session.Metadata.TryGetValue(BillIdMetadataKey, out var sessionBillId) &&
               Guid.TryParse(sessionBillId, out var parsedBillId) &&
               parsedBillId == billId &&
               session.Metadata.TryGetValue(
                   UserProfileIdMetadataKey,
                   out var sessionUserProfileId) &&
               Guid.TryParse(sessionUserProfileId, out var parsedUserProfileId) &&
               parsedUserProfileId == userProfileId;
    }

    private string GetSecretKey()
    {
        var secretKey = _configuration["Stripe:SecretKey"];

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "Stripe secret key is not configured.");
        }

        return secretKey;
    }

    private static string ValidateBaseUrl(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
             uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException(
                "A valid application URL is required.",
                nameof(baseUrl));
        }

        return baseUrl.TrimEnd('/');
    }

    private static string NormalizeCurrency(string currency)
    {
        var normalized = currency?.Trim().ToUpperInvariant();

        if (normalized is not ("CAD" or "USD" or "EUR" or "GBP" or "JPY"))
        {
            throw new ArgumentException(
                "The bill currency is not supported for payment.",
                nameof(currency));
        }

        return normalized;
    }

    private static long ToMinorUnits(decimal amount, string currency)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Payment amount must be greater than zero.");
        }

        var multiplier = currency == "JPY" ? 1m : 100m;
        return checked((long)decimal.Round(
            amount * multiplier,
            0,
            MidpointRounding.AwayFromZero));
    }
}
