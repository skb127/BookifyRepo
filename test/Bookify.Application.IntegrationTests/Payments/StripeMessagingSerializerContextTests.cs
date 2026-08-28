using System.Text.Json;
using Bookify.Infrastructure.Messaging;
using FluentAssertions;

namespace Bookify.Application.IntegrationTests.Payments;

public class StripeMessagingSerializerContextTests
{
    [Fact]
    public void Deserialize_ShouldDeserializeCamelCaseJson_Successfully()
    {
        // Arrange
        const string json = """
                            {
                              "eventType" : "checkout.session.completed",
                              "bookingId" : "a9b7c8d9-1111-2222-3333-444455556689",
                              "sessionId" : "cs_test_a1b2c3d4e5f6g7h8i9j0k1l2m3",
                              "paymentIntentId" : "pi_3MtwBwLkdIwHu7ix28a3tdRt",
                              "isInstant" : true
                            }
                            """;

        // Act
        StripeWebhookEvent? result = JsonSerializer.Deserialize(
            json,
            StripeMessagingSerializerContext.Default.StripeWebhookEvent);

        // Assert
        result.Should().NotBeNull();
        result.EventType.Should().Be("checkout.session.completed");
        result.BookingId.Should().Be(Guid.Parse("a9b7c8d9-1111-2222-3333-444455556689"));
        result.SessionId.Should().Be("cs_test_a1b2c3d4e5f6g7h8i9j0k1l2m3");
        result.PaymentIntentId.Should().Be("pi_3MtwBwLkdIwHu7ix28a3tdRt");
        result.IsInstant.Should().BeTrue();
        result.RefundId.Should().BeNull();
        result.Amount.Should().Be(0);
        result.FailureReason.Should().BeNull();
    }

    [Fact]
    public void Deserialize_ShouldDeserializePascalCaseJson_Successfully()
    {
        // Arrange
        const string json = """
                            {
                              "EventType" : "charge.refunded",
                              "BookingId" : "a9b7c8d9-1111-2222-3333-444455556689",
                              "RefundId" : "re_12345",
                              "Amount" : 150.50
                            }
                            """;

        // Act
        StripeWebhookEvent? result = JsonSerializer.Deserialize(
            json,
            StripeMessagingSerializerContext.Default.StripeWebhookEvent);

        // Assert
        result.Should().NotBeNull();
        result.EventType.Should().Be("charge.refunded");
        result.BookingId.Should().Be(Guid.Parse("a9b7c8d9-1111-2222-3333-444455556689"));
        result.RefundId.Should().Be("re_12345");
        result.Amount.Should().Be(150.50m);
    }
}
