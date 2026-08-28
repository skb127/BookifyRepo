using Bookify.Domain.Abstractions;
using Bookify.Domain.Shared;

namespace Bookify.Domain.Bookings;

public sealed class Transaction : Entity
{
    private Transaction(
        Guid id,
        Guid bookingId,
        string stripeSessionId,
        string? stripePaymentIntentId,
        string checkoutSessionUrl,
        Money amount,
        string providerStatus,
        DateTime createdOnUtc)
        : base(id)
    {
        BookingId = bookingId;
        StripeSessionId = stripeSessionId;
        StripePaymentIntentId = stripePaymentIntentId;
        CheckoutSessionUrl = checkoutSessionUrl;
        Amount = amount;
        ProviderStatus = providerStatus;
        CreatedOnUtc = createdOnUtc;
    }

    private Transaction()
    {
    }

    public Guid BookingId { get; private set; }
    public string StripeSessionId { get; private set; } = null!;
    public string? StripePaymentIntentId { get; private set; }

    public string CheckoutSessionUrl { get; private set; } = null!;
    public Money Amount { get; private set; } = null!;
    public string ProviderStatus { get; private set; } = null!;
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? UpdatedOnUtc { get; private set; }

    public static Transaction Create(
        Guid bookingId,
        string stripeSessionId,
        string? stripePaymentIntentId,
        string checkoutSessionUrl,
        Money amount,
        string providerStatus,
        DateTime utcNow) =>
        new(
            Guid.CreateVersion7(),
            bookingId,
            stripeSessionId,
            stripePaymentIntentId,
            checkoutSessionUrl,
            amount,
            providerStatus,
            utcNow);

    public void UpdateStatus(string newStatus, string? paymentIntentId, DateTime utcNow)
    {
        ProviderStatus = newStatus;
        if (paymentIntentId is not null)
        {
            StripePaymentIntentId = paymentIntentId;
        }
        UpdatedOnUtc = utcNow;
    }
}
