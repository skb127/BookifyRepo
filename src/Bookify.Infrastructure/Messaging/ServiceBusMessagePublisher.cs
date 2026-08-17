using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Bookify.Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace Bookify.Infrastructure.Messaging;

internal sealed class ServiceBusMessagePublisher : IMessagePublisher
{
    private readonly ServiceBusClient? _client;
    private readonly ILogger<ServiceBusMessagePublisher> _logger;

    public ServiceBusMessagePublisher(
        ServiceBusClient? client,
        ILogger<ServiceBusMessagePublisher> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task PublishAsync<T>(string queueName, T message, CancellationToken cancellationToken = default)
        where T : class
    {
        if (_client is null)
        {
            _logger.LogWarning("ServiceBusClient is not configured. Bypassing message publication for queue {QueueName}.", queueName);
            return;
        }

        try
        {
            ServiceBusSender sender = _client.CreateSender(queueName);
            string jsonBody = JsonSerializer.Serialize(message);
            var serviceBusMessage = new ServiceBusMessage(jsonBody)
            {
                ContentType = "application/json"
            };

            _logger.LogInformation("Publishing message of type {MessageType} to queue {QueueName}...", typeof(T).Name, queueName);
            await sender.SendMessageAsync(serviceBusMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message of type {MessageType} to queue {QueueName}.", typeof(T).Name, queueName);
            throw;
        }
    }
}
