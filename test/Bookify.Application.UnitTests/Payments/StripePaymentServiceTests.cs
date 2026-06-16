using Bookify.Application.Abstractions.Payments;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Payments;

public class StripePaymentServiceTests
{
    private readonly IPaymentGateway _paymentGatewayMock;

    public StripePaymentServiceTests()
    {
        _paymentGatewayMock = Substitute.For<IPaymentGateway>();
    }

    [Fact]
    public async Task CreateCheckoutSession_ShouldReturnResult_WhenGatewaySucceeds()
    {
        // Arrange
        var request = new CreateCheckoutSessionRequest(
            Guid.NewGuid(),
            "cus_12345",
            120.00m,
            "usd",
            "https://success.com",
            "https://cancel.com",
            DateTime.UtcNow.AddHours(1),
            "Luxury Apartment",
            "A beautiful apartment in the city center");

        var expectedResult = new CreateCheckoutSessionResult(
            "cs_test_12345",
            "https://checkout.stripe.com/pay/cs_test_12345",
            "pi_12345");

        _paymentGatewayMock.CreateCheckoutSessionAsync(request, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act
        var result = await _paymentGatewayMock.CreateCheckoutSessionAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SessionId.Should().Be(expectedResult.SessionId);
        result.CheckoutUrl.Should().Be(expectedResult.CheckoutUrl);
        result.PaymentIntentId.Should().Be(expectedResult.PaymentIntentId);

        await _paymentGatewayMock.Received(1).CreateCheckoutSessionAsync(request, CancellationToken.None);
    }

    [Fact]
    public async Task CancelPaymentIntent_ShouldReturnTrue_WhenGatewaySucceeds()
    {
        // Arrange
        const string paymentIntentId = "pi_12345";
        _paymentGatewayMock.CancelPaymentIntentAsync(paymentIntentId, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _paymentGatewayMock.CancelPaymentIntentAsync(paymentIntentId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        await _paymentGatewayMock.Received(1).CancelPaymentIntentAsync(paymentIntentId, CancellationToken.None);
    }

    [Fact]
    public async Task CreateRefund_ShouldReturnRefundResult_WhenGatewaySucceeds()
    {
        // Arrange
        const string paymentIntentId = "pi_12345";
        const decimal amount = 50.00m;
        const string currency = "usd";
        var expectedResult = new RefundResult("re_12345", "succeeded", amount);

        _paymentGatewayMock.CreateRefundAsync(paymentIntentId, amount, currency, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act
        var result = await _paymentGatewayMock.CreateRefundAsync(paymentIntentId, amount, currency, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RefundId.Should().Be(expectedResult.RefundId);
        result.Status.Should().Be(expectedResult.Status);
        result.Amount.Should().Be(expectedResult.Amount);

        await _paymentGatewayMock.Received(1).CreateRefundAsync(paymentIntentId, amount, currency, CancellationToken.None);
    }
}
