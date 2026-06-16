using Bookify.Application.Abstractions.Payments;
using Stripe;
using Stripe.Checkout;

namespace Bookify.Infrastructure.Payments;

internal sealed class StripePaymentService : IPaymentGateway
{
    private readonly IStripeClient _stripeClient;

    public StripePaymentService(IStripeClient stripeClient) => _stripeClient = stripeClient;

    public async Task<CreateCheckoutSessionResult> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var service = new SessionService(_stripeClient);

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = ["card"],
            Mode = "payment",
            Customer = request.StripeCustomerId,
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            ExpiresAt = request.ExpiresAt,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency,
                        UnitAmount = (long)(request.TotalAmount * 100),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = request.ApartmentName,
                            Description = request.ApartmentDescription
                        }
                    },
                    Quantity = 1
                }
            ],
            Metadata = new Dictionary<string, string>
            {
                { "booking_id", request.BookingId.ToString() }
            }
        };

        Session session = await service.CreateAsync(options, cancellationToken: cancellationToken);

        return new CreateCheckoutSessionResult(
            session.Id,
            session.Url,
            session.PaymentIntentId
        );
    }

    public async Task<bool> CancelPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        var service = new PaymentIntentService(_stripeClient);

        try
        {
            PaymentIntent paymentIntent =
                await service.CancelAsync(paymentIntentId, cancellationToken: cancellationToken);
            return paymentIntent.Status == "canceled";
        }
        catch (StripeException)
        {
            return false;
        }
    }

    public async Task<RefundResult> CreateRefundAsync(
        string paymentIntentId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        var service = new RefundService(_stripeClient);

        var options = new RefundCreateOptions
        {
            PaymentIntent = paymentIntentId,
            Amount = (long)(amount * 100),
            Metadata = new Dictionary<string, string>
            {
                { "currency", currency }
            }
        };

        Refund refund = await service.CreateAsync(options, cancellationToken: cancellationToken);

        decimal refundedAmount = refund.Amount / 100m;

        return new RefundResult(
            refund.Id,
            refund.Status,
            refundedAmount
        );
    }
}
