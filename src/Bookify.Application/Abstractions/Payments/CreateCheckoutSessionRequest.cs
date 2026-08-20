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
