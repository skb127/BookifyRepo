namespace Bookify.Application.Abstractions.Payments;

public record CreateCheckoutSessionResult(
    string SessionId,
    string CheckoutUrl,
    string? PaymentIntentId);
