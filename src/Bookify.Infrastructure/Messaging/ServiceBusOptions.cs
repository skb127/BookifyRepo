namespace Bookify.Infrastructure.Messaging;

public sealed class ServiceBusOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = "stripe-events";
}
