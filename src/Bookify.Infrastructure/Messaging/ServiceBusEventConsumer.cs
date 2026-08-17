using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Payments.CompleteRefund;
using Bookify.Application.Payments.ConfirmPayment;
using Bookify.Application.Payments.ExpireCheckoutSession;
using Bookify.Application.Payments.FailRefund;
using Bookify.Domain.Abstractions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bookify.Infrastructure.Messaging;

internal sealed class ServiceBusEventConsumer : BackgroundService, IEventBusConsumer
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceBusOptions _options;
    private readonly ServiceBusQueuesOptions _queuesOptions;
    private readonly ILogger<ServiceBusEventConsumer> _logger;
    private readonly ServiceBusClient? _client;
    private ServiceBusProcessor? _processor;

    public ServiceBusEventConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<ServiceBusOptions> options,
        IOptions<ServiceBusQueuesOptions> queuesOptions,
        ILogger<ServiceBusEventConsumer> logger,
        ServiceBusClient? client = null)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _queuesOptions = queuesOptions.Value;
        _logger = logger;
        _client = client;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_client is null || string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            _logger.LogWarning("Service Bus ConnectionString or Client is not configured. Bypassing event consumer initialization.");
            return;
        }

        try
        {
            _processor = _client.CreateProcessor(_queuesOptions.StripeEvents, new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1
            });

            _processor.ProcessMessageAsync += ProcessMessageAsync;
            _processor.ProcessErrorAsync += ProcessErrorAsync;

            _logger.LogInformation("Starting Service Bus Processor for queue {QueueName}...", _queuesOptions.StripeEvents);
            await _processor.StartProcessingAsync(stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // Ignored, normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to initialize or run Azure Service Bus Processor.");
        }
        finally
        {
            if (_processor is not null)
            {
                try
                {
                    await _processor.StopProcessingAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping Service Bus processor.");
                }

                _processor.ProcessMessageAsync -= ProcessMessageAsync;
                _processor.ProcessErrorAsync -= ProcessErrorAsync;
                await _processor.DisposeAsync();
            }
        }
    }

    public override void Dispose()
    {
        _processor?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        string messageId = args.Message.MessageId;
        _logger.LogInformation("Processing Service Bus message {MessageId}...", messageId);

        try
        {
            StripeWebhookEvent? stripeEvent = JsonSerializer.Deserialize(
                args.Message.Body.ToMemory().Span,
                StripeMessagingSerializerContext.Default.StripeWebhookEvent);

            if (stripeEvent is null)
            {
                _logger.LogWarning("Message {MessageId} parsed to null. Completing message to drop it.", messageId);
                await args.CompleteMessageAsync(args.Message);
                return;
            }

            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            Result result = stripeEvent.EventType switch
            {
                "checkout.session.completed" => await sender.Send(new ConfirmPaymentCommand(
                    stripeEvent.BookingId,
                    stripeEvent.SessionId ?? string.Empty,
                    stripeEvent.PaymentIntentId ?? string.Empty,
                    stripeEvent.IsInstant), args.CancellationToken),

                "checkout.session.expired" => await sender.Send(new ExpireStripeSessionCommand(
                    stripeEvent.BookingId,
                    stripeEvent.SessionId ?? string.Empty), args.CancellationToken),

                "charge.refunded" => await sender.Send(new CompleteRefundCommand(
                    stripeEvent.BookingId,
                    stripeEvent.RefundId ?? string.Empty,
                    stripeEvent.Amount), args.CancellationToken),

                "refund.failed" => await sender.Send(new FailRefundCommand(
                    stripeEvent.BookingId,
                    stripeEvent.RefundId ?? string.Empty,
                    stripeEvent.FailureReason ?? string.Empty), args.CancellationToken),

                _ => Result.Failure(new Error("ServiceBus.UnknownEvent", $"Unknown event type: {stripeEvent.EventType}"))
            };

            if (result.IsSuccess)
            {
                _logger.LogInformation("Successfully processed message {MessageId} for event {EventType}.", messageId, stripeEvent.EventType);
                await args.CompleteMessageAsync(args.Message);
            }
            else
            {
                _logger.LogWarning("Failed to process message {MessageId} for event {EventType}. Error: {Error}. Message will be abandoned to retry.",
                    messageId, stripeEvent.EventType, result.Error);
                await args.AbandonMessageAsync(args.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Service Bus message {MessageId}. Abandoning message.", messageId);
            await args.AbandonMessageAsync(args.Message);
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Service Bus processor encountered an error in source {ErrorSource}. Entity Path: {EntityPath}.", args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }
}
