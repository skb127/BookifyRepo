using System.Text.Json;

namespace SendEvent;

class Program
{
    static async Task Main(string[] args)
    {
        const string connectionString = "Endpoint=sb://localhost:5672;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
        const string queueName = "stripe-events";

        Console.WriteLine("Initializing Service Bus Client...");
        await using var client = new ServiceBusClient(connectionString);
        await using var sender = client.CreateSender(queueName);

        var webhookEvent = new
        {
            EventType = "checkout.session.completed",
            BookingId = Guid.Parse("a8e3d0fa-4d1a-471a-bc01-e945fa6712ab"),
            SessionId = "session_manual_test_123",
            PaymentIntentId = "intent_manual_test_123",
            IsInstant = true
        };

        string jsonPayload = JsonSerializer.Serialize(webhookEvent);
        Console.WriteLine($"Constructed JSON Payload:\n{jsonPayload}");

        var message = new ServiceBusMessage(BinaryData.FromString(jsonPayload))
        {
            MessageId = Guid.NewGuid().ToString()
        };

        Console.WriteLine("Sending message to the queue...");
        await sender.SendMessageAsync(message);
        Console.WriteLine("Message sent successfully!");
    }
}
