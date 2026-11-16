
using Stripe;
using Stripe.Checkout;

namespace RentConnect.Services;

public class StripePaymentService
{
    private readonly IConfiguration _configuration;

    public StripePaymentService(
        IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<string> CreateCheckoutSessionAsync(
        long amountInCents,
        string paymentId,
        string baseUrl)
    {
        if (amountInCents <= 0)
        {
            throw new ArgumentException(
                "Payment amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(paymentId))
        {
            throw new ArgumentException(
                "Payment ID is required.");
        }

        if (!Uri.TryCreate(
            baseUrl,
            UriKind.Absolute,
            out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
             uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException(
                "A valid application URL is required.");
        }

        var secretKey = _configuration["Stripe:SecretKey"];

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "Stripe secret key is not configured.");
        }

        var options = new SessionCreateOptions
        {
            Mode = "payment",

            ClientReferenceId = paymentId,

            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "cad",

                        UnitAmount = amountInCents,

                        ProductData =
                            new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "RentConnect Monthly Rent"
                            }
                    },

                    Quantity = 1
                }
            },

            SuccessUrl =
                $"{baseUrl.TrimEnd('/')}/payments/success?session_id={{CHECKOUT_SESSION_ID}}",

            CancelUrl =
                $"{baseUrl.TrimEnd('/')}/payments/cancel"
        };

        var service = new SessionService();

        var session = await service.CreateAsync(
            options,
            new RequestOptions
            {
                ApiKey = secretKey
            });

        if (string.IsNullOrWhiteSpace(session.Url))
        {
            throw new InvalidOperationException(
                "Stripe Checkout URL was not returned.");
        }

        return session.Url;
    }
}
