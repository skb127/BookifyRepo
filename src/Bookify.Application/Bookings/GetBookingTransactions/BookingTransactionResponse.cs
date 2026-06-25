namespace Bookify.Application.Bookings.GetBookingTransactions;

public sealed class BookingTransactionResponse
{
    public Guid Id { get; init; }
    public Guid BookingId { get; init; }
    public string StripeSessionId { get; init; } = "";
    public string? StripePaymentIntentId { get; init; }
    public string CheckoutSessionUrl { get; init; } = "";
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "";
    public string ProviderStatus { get; init; } = "";
    public DateTime CreatedOnUtc { get; init; }
    public DateTime? UpdatedOnUtc { get; init; }
}
