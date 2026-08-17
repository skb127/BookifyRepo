using System.Text.Json.Serialization;

namespace Bookify.Infrastructure.Messaging;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(StripeWebhookEvent))]
internal sealed partial class StripeMessagingSerializerContext : JsonSerializerContext;
