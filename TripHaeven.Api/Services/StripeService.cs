using Stripe;
using Stripe.Checkout;

namespace TripHaeven.Api.Services;

public class StripeService
{
    private readonly string _stripeSecretKey;

    public StripeService(IConfiguration configuration)
    {
        _stripeSecretKey = configuration["Stripe:SecretKey"] ?? configuration["STRIPE_SECRET_KEY"] ?? Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        StripeConfiguration.ApiKey = _stripeSecretKey;
    }

    public async Task<Session> CreateCheckoutSessionAsync(string bookingId, decimal amount, string hotelName, string successUrl, string cancelUrl)
    {
        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = hotelName,
                            Description = $"Booking ID: {bookingId}"
                        },
                        UnitAmount = (long)(amount * 100), // Amount in cents
                    },
                    Quantity = 1,
                },
            },
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string>
            {
                { "bookingId", bookingId }
            }
        };

        var service = new SessionService();
        return await service.CreateAsync(options);
    }
}
