namespace Bookify.Infrastructure.Messaging;

internal sealed class StripeWebhookEvent
{
    public string EventType { get; init; } = string.Empty;
    public Guid BookingId { get; init; }
    public string? SessionId { get; init; }
    public string? PaymentIntentId { get; init; }
    public bool IsInstant { get; init; }
    public string? RefundId { get; init; }
    public decimal Amount { get; init; }
    public string? FailureReason { get; init; }
}
