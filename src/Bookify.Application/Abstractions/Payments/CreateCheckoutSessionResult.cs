#pragma warning disable CA1054 // Uri parameters should not be strings
#pragma warning disable CA1056 // Uri properties should not be strings

namespace Bookify.Application.Abstractions.Payments;

public record CreateCheckoutSessionResult(
    string SessionId,
    string CheckoutUrl,
    string? PaymentIntentId);
