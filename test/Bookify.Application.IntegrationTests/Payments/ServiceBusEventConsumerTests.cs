using Azure.Messaging.ServiceBus;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.Bookings.GetBooking;
using Bookify.Domain.Bookings;
using Bookify.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bookify.Application.IntegrationTests.Payments;

public class ServiceBusEventConsumerTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory _factory;

    public ServiceBusEventConsumerTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EventConsumer_ShouldProcessCheckoutSessionCompletedEvent_FromQueue()
    {
        // Arrange
        var setupResult = await BookingTestHelpers.SetupPendingPaymentBookingAsync(this);
        Guid bookingId = setupResult.bookingId;
        string guestToken = setupResult.guestToken;

        var options = _factory.Services.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
        var queuesOptions = _factory.Services.GetRequiredService<IOptions<ServiceBusQueuesOptions>>().Value;

        var sessionId = $"session_{Guid.NewGuid()}";
        var paymentIntentId = $"intent_{Guid.NewGuid()}";

        var webhookEvent = new StripeWebhookEvent
        {
            EventType = "checkout.session.completed",
            BookingId = bookingId,
            SessionId = sessionId,
            PaymentIntentId = paymentIntentId,
            IsInstant = true
        };

        var messageBody = BinaryData.FromObjectAsJson(webhookEvent);
        var message = new ServiceBusMessage(messageBody)
        {
            MessageId = Guid.NewGuid().ToString()
        };

        // Act
        var client = new ServiceBusClient(options.ConnectionString);
        try
        {
            var sender = client.CreateSender(queuesOptions.StripeEvents);
            await sender.SendMessageAsync(message);
        }
        finally
        {
            await client.DisposeAsync();
        }

        // Assert
        BookingResponse? updatedBooking = null;
        for (int i = 0; i < 30; i++)
        {
            try
            {
                updatedBooking = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, guestToken);

                if (updatedBooking.Status == (int)BookingStatus.Confirmed)
                {
                    break;
                }
            }
            catch (HttpRequestException)
            {
                // Ignored
            }

            await Task.Delay(500);
        }

        updatedBooking.Should().NotBeNull();
        updatedBooking.Status.Should().Be((int)BookingStatus.Confirmed);
        updatedBooking.PaymentStatus.Should().Be((int)PaymentStatus.Paid);
    }

    [Fact]
    public async Task EventConsumer_ShouldProcessCheckoutSessionExpiredEvent_FromQueue()
    {
        // Arrange
        var setupResult = await BookingTestHelpers.SetupPendingPaymentBookingAsync(this);
        Guid bookingId = setupResult.bookingId;
        string guestToken = setupResult.guestToken;

        var options = _factory.Services.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
        var queuesOptions = _factory.Services.GetRequiredService<IOptions<ServiceBusQueuesOptions>>().Value;

        var sessionId = $"session_{Guid.NewGuid()}";

        var webhookEvent = new StripeWebhookEvent
        {
            EventType = "checkout.session.expired",
            BookingId = bookingId,
            SessionId = sessionId
        };

        var messageBody = BinaryData.FromObjectAsJson(webhookEvent);
        var message = new ServiceBusMessage(messageBody)
        {
            MessageId = Guid.NewGuid().ToString()
        };

        // Act
        var client = new ServiceBusClient(options.ConnectionString);
        try
        {
            var sender = client.CreateSender(queuesOptions.StripeEvents);
            await sender.SendMessageAsync(message);
        }
        finally
        {
            await client.DisposeAsync();
        }

        // Assert
        BookingResponse? updatedBooking = null;
        for (int i = 0; i < 30; i++)
        {
            try
            {
                updatedBooking = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, guestToken);

                if (updatedBooking.Status == (int)BookingStatus.Expired)
                {
                    break;
                }
            }
            catch (HttpRequestException)
            {
                // Ignored
            }

            await Task.Delay(500);
        }

        updatedBooking.Should().NotBeNull();
        updatedBooking.Status.Should().Be((int)BookingStatus.Expired);
    }

    [Fact]
    public async Task EventConsumer_ShouldProcessChargeRefundedEvent_FromQueue()
    {
        // Arrange
        var setupResult = await BookingTestHelpers.SetupRefundProcessingBookingAsync(this);
        Guid bookingId = setupResult.bookingId;
        string guestToken = setupResult.guestToken;

        var options = _factory.Services.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
        var queuesOptions = _factory.Services.GetRequiredService<IOptions<ServiceBusQueuesOptions>>().Value;

        var refundId = $"refund_{Guid.NewGuid()}";

        var webhookEvent = new StripeWebhookEvent
        {
            EventType = "charge.refunded",
            BookingId = bookingId,
            RefundId = refundId,
            Amount = 100
        };

        var messageBody = BinaryData.FromObjectAsJson(webhookEvent);
        var message = new ServiceBusMessage(messageBody)
        {
            MessageId = Guid.NewGuid().ToString()
        };

        // Act
        var client = new ServiceBusClient(options.ConnectionString);
        try
        {
            var sender = client.CreateSender(queuesOptions.StripeEvents);
            await sender.SendMessageAsync(message);
        }
        finally
        {
            await client.DisposeAsync();
        }

        // Assert
        BookingResponse? updatedBooking = null;
        for (int i = 0; i < 30; i++)
        {
            try
            {
                updatedBooking = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, guestToken);

                if (updatedBooking.PaymentStatus == (int)PaymentStatus.Refunded)
                {
                    break;
                }
            }
            catch (HttpRequestException)
            {
                // Ignored
            }

            await Task.Delay(500);
        }

        updatedBooking.Should().NotBeNull();
        updatedBooking.PaymentStatus.Should().Be((int)PaymentStatus.Refunded);
    }

    [Fact]
    public async Task EventConsumer_ShouldProcessRefundFailedEvent_FromQueue()
    {
        // Arrange
        var setupResult = await BookingTestHelpers.SetupRefundProcessingBookingAsync(this);
        Guid bookingId = setupResult.bookingId;
        string guestToken = setupResult.guestToken;

        var options = _factory.Services.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
        var queuesOptions = _factory.Services.GetRequiredService<IOptions<ServiceBusQueuesOptions>>().Value;

        var refundId = $"refund_{Guid.NewGuid()}";

        var webhookEvent = new StripeWebhookEvent
        {
            EventType = "refund.failed",
            BookingId = bookingId,
            RefundId = refundId,
            FailureReason = "Card expired"
        };

        var messageBody = BinaryData.FromObjectAsJson(webhookEvent);
        var message = new ServiceBusMessage(messageBody)
        {
            MessageId = Guid.NewGuid().ToString()
        };

        // Act
        var client = new ServiceBusClient(options.ConnectionString);
        try
        {
            var sender = client.CreateSender(queuesOptions.StripeEvents);
            await sender.SendMessageAsync(message);
        }
        finally
        {
            await client.DisposeAsync();
        }

        // Assert
        BookingResponse? updatedBooking = null;
        for (int i = 0; i < 30; i++)
        {
            try
            {
                updatedBooking = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, guestToken);

                if (updatedBooking.PaymentStatus == (int)PaymentStatus.Paid)
                {
                    break;
                }
            }
            catch (HttpRequestException)
            {
                // Ignored
            }

            await Task.Delay(500);
        }

        updatedBooking.Should().NotBeNull();
        updatedBooking.PaymentStatus.Should().Be((int)PaymentStatus.Paid);
    }
}
