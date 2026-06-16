namespace Bookify.Infrastructure.Payments;

public sealed class StripeOptions
{
    public string SecretKey { get; set; } = string.Empty;

    public string PublishableKey { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;
}
