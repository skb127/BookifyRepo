using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.Payments.ConfirmPayment;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using FluentAssertions;

namespace Bookify.Application.IntegrationTests.Payments;

public class ConfirmPaymentCommandHandlerTests : BaseIntegrationTest
{
    public ConfirmPaymentCommandHandlerTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingNotFound()
    {
        // Arrange
        var command = new ConfirmPaymentCommand(
            Guid.NewGuid(),
            $"session_{Guid.NewGuid()}",
            $"intent_{Guid.NewGuid()}",
            IsInstantBooking: true);

        // Act
        Result result = await Sender.Send(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldConfirmBookingAndMarkAsPaid_WhenInstantBooking()
    {
        // Arrange
        var setupResult = await BookingTestHelpers.SetupPendingPaymentBookingAsync(this);
        Guid bookingId = setupResult.bookingId;
        string guestToken = setupResult.guestToken;

        var sessionId = $"session_{Guid.NewGuid()}";
        var paymentIntentId = $"intent_{Guid.NewGuid()}";
        var command = new ConfirmPaymentCommand(
            bookingId,
            sessionId,
            paymentIntentId,
            IsInstantBooking: true);

        // Act
        Result result = await Sender.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedBooking = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, guestToken);
        updatedBooking.Should().NotBeNull();
        updatedBooking.Status.Should().Be((int)BookingStatus.Confirmed);
        updatedBooking.PaymentStatus.Should().Be((int)PaymentStatus.Paid);

        string adminToken = await GetAdminTokenAsync();
        var transactions = await BookingTestHelpers.GetBookingTransactionsViaApiAsync(this, bookingId, adminToken);
        transactions.Should().NotBeNull().And.NotBeEmpty();
        var transaction = transactions.FirstOrDefault(t => t.StripeSessionId == sessionId);
        transaction.Should().NotBeNull();
        transaction.StripePaymentIntentId.Should().Be(paymentIntentId);
        transaction.ProviderStatus.Should().Be("paid");
    }

    [Fact]
    public async Task Handle_ShouldReserveBookingAndAuthorizePayment_WhenNonInstantBooking()
    {
        // Arrange
        var setupResult = await BookingTestHelpers.SetupPendingPaymentBookingAsync(this);
        Guid bookingId = setupResult.bookingId;
        string guestToken = setupResult.guestToken;

        var sessionId = $"session_{Guid.NewGuid()}";
        var paymentIntentId = $"intent_{Guid.NewGuid()}";
        var command = new ConfirmPaymentCommand(
            bookingId,
            sessionId,
            paymentIntentId,
            IsInstantBooking: false);

        // Act
        Result result = await Sender.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedBooking = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, guestToken);
        updatedBooking.Should().NotBeNull();
        updatedBooking.Status.Should().Be((int)BookingStatus.Reserved);
        updatedBooking.PaymentStatus.Should().Be((int)PaymentStatus.Authorized);

        string adminToken = await GetAdminTokenAsync();
        var transactions = await BookingTestHelpers.GetBookingTransactionsViaApiAsync(this, bookingId, adminToken);
        transactions.Should().NotBeNull().And.NotBeEmpty();
        var transaction = transactions.FirstOrDefault(t => t.StripeSessionId == sessionId);
        transaction.Should().NotBeNull();
        transaction.StripePaymentIntentId.Should().Be(paymentIntentId);
        transaction.ProviderStatus.Should().Be("authorized");
    }

    [Fact]
    public async Task Handle_ShouldBeIdempotent_WhenAlreadyProcessed()
    {
        // Arrange
        var setupResult = await BookingTestHelpers.SetupPendingPaymentBookingAsync(this);
        Guid bookingId = setupResult.bookingId;

        var sessionId = $"session_{Guid.NewGuid()}";
        var paymentIntentId = $"intent_{Guid.NewGuid()}";
        var command = new ConfirmPaymentCommand(
            bookingId,
            sessionId,
            paymentIntentId,
            IsInstantBooking: true);

        // Act - 1st call
        Result result1 = await Sender.Send(command);
        result1.IsSuccess.Should().BeTrue();

        // Act - 2nd call (idempotent)
        Result result2 = await Sender.Send(command);

        // Assert
        result2.IsSuccess.Should().BeTrue();
    }
}
