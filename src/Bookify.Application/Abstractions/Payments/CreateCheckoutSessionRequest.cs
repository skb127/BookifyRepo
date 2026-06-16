#pragma warning disable CA1054 // Uri parameters should not be strings
#pragma warning disable CA1056 // Uri properties should not be strings

namespace Bookify.Application.Abstractions.Payments;

public record CreateCheckoutSessionRequest(
    Guid BookingId,
    string StripeCustomerId,
    decimal TotalAmount,
    string Currency,
    string SuccessUrl,
    string CancelUrl,
    DateTime ExpiresAt,
    string ApartmentName,
    string ApartmentDescription);
