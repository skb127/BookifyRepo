namespace Bookify.Application.Abstractions.Messaging;

public sealed class ServiceBusQueuesOptions
{
    public const string SectionName = "ServiceBus:Queues";

    public string StripeEvents { get; set; } = "stripe-events";
    public string InvoiceRequests { get; set; } = "invoice-requests";
}
