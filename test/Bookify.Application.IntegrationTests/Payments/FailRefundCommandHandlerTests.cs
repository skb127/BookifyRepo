using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.Payments.FailRefund;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using FluentAssertions;

namespace Bookify.Application.IntegrationTests.Payments;

public class FailRefundCommandHandlerTests : BaseIntegrationTest
{
    public FailRefundCommandHandlerTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingNotFound()
    {
        // Arrange
        var command = new FailRefundCommand(Guid.NewGuid(), "refund_xyz", "Card declined");

        // Act
        Result result = await Sender.Send(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldRevertRefundFailure_WhenRefundProcessing()
    {
        // Arrange
        var setupResult = await BookingTestHelpers.SetupRefundProcessingBookingAsync(this);
        Guid bookingId = setupResult.bookingId;
        string guestToken = setupResult.guestToken;

        var command = new FailRefundCommand(bookingId, "refund_xyz", "Card expired");

        // Act
        Result result = await Sender.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedBooking = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, guestToken);
        updatedBooking.Should().NotBeNull();
        updatedBooking.PaymentStatus.Should().Be((int)PaymentStatus.Paid);
    }

    [Fact]
    public async Task Handle_ShouldBeIdempotent_WhenNotRefundProcessing()
    {
        // Arrange
        var setupResult = await BookingTestHelpers.SetupCancelledPaidBookingAsync(this);
        Guid bookingId = setupResult.bookingId;

        var command = new FailRefundCommand(bookingId, "refund_xyz", "Card expired");

        // Act
        Result result = await Sender.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
