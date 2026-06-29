using Bookify.Application.Abstractions.Payments;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;

namespace Bookify.Infrastructure.Payments;

internal sealed class StripePaymentService : IPaymentGateway
{
    private readonly IStripeClient _stripeClient;
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(IStripeClient stripeClient, ILogger<StripePaymentService> logger)
    {
        _stripeClient = stripeClient;
        _logger = logger;
    }

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
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error cancelling payment intent {PaymentIntentId}.", paymentIntentId);
            return false;
        }
    }

    public async Task<bool> CapturePaymentIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        var service = new PaymentIntentService(_stripeClient);

        try
        {
            var options = new PaymentIntentCaptureOptions();
            PaymentIntent paymentIntent =
                await service.CaptureAsync(paymentIntentId, options, cancellationToken: cancellationToken);
            return paymentIntent.Status == "succeeded";
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error capturing payment intent {PaymentIntentId}.", paymentIntentId);
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

        try
        {
            Refund refund = await service.CreateAsync(options, cancellationToken: cancellationToken);

            decimal refundedAmount = refund.Amount / 100m;

            return new RefundResult(
                refund.Id,
                refund.Status,
                refundedAmount
            );
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error creating refund for payment intent {PaymentIntentId}.", paymentIntentId);
            throw new InvalidOperationException($"Stripe refund creation failed for payment intent {paymentIntentId}.", ex);
        }
    }
}
