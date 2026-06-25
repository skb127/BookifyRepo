using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.Payments.ExpireCheckoutSession;
using Bookify.Application.Payments.ConfirmPayment;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using FluentAssertions;

namespace Bookify.Application.IntegrationTests.Payments;

public class ExpireStripeSessionCommandHandlerTests : BaseIntegrationTest
{
    public ExpireStripeSessionCommandHandlerTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingNotFound()
    {
        // Arrange
        var command = new ExpireStripeSessionCommand(Guid.NewGuid(), "session_xyz");

        // Act
        Result result = await Sender.Send(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldExpireBookingAndTransaction_WhenPendingPayment()
    {
        // Arrange
        var setupResult = await BookingTestHelpers.SetupPendingPaymentBookingAsync(this);
        Guid bookingId = setupResult.bookingId;
        string guestToken = setupResult.guestToken;

        var sessionId = $"session_{Guid.NewGuid()}";
        var paymentIntentId = $"intent_{Guid.NewGuid()}";

        // Create a transaction by calling ConfirmPayment command (non-instant)
        var confirmCommand = new ConfirmPaymentCommand(
            bookingId,
            sessionId,
            paymentIntentId,
            IsInstantBooking: false);
        var confirmResult = await Sender.Send(confirmCommand);
        confirmResult.IsSuccess.Should().BeTrue();

        var command = new ExpireStripeSessionCommand(bookingId, sessionId);

        // Act
        Result result = await Sender.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedBooking = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, guestToken);
        updatedBooking.Should().NotBeNull();
        updatedBooking.Status.Should().Be((int)BookingStatus.Expired);

        string adminToken = await GetAdminTokenAsync();
        var transactions = await BookingTestHelpers.GetBookingTransactionsViaApiAsync(this, bookingId, adminToken);
        transactions.Should().NotBeNull().And.NotBeEmpty();
        var transaction = transactions.FirstOrDefault(t => t.StripeSessionId == sessionId);
        transaction.Should().NotBeNull();
        transaction.ProviderStatus.Should().Be("expired");
    }

    [Fact]
    public async Task Handle_ShouldBeIdempotent_WhenAlreadyExpired()
    {
        // Arrange
        var setupResult = await BookingTestHelpers.SetupPendingPaymentBookingAsync(this);
        Guid bookingId = setupResult.bookingId;

        var command = new ExpireStripeSessionCommand(bookingId, "session_xyz");

        // Act - 1st call
        Result result1 = await Sender.Send(command);
        result1.IsSuccess.Should().BeTrue();

        // Act - 2nd call
        Result result2 = await Sender.Send(command);

        // Assert
        result2.IsSuccess.Should().BeTrue();
    }
}
