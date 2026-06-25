using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.Payments.CompleteRefund;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using FluentAssertions;

namespace Bookify.Application.IntegrationTests.Payments;

public class CompleteRefundCommandHandlerTests : BaseIntegrationTest
{
    public CompleteRefundCommandHandlerTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingNotFound()
    {
        // Arrange
        var command = new CompleteRefundCommand(Guid.NewGuid(), "refund_xyz", 100);

        // Act
        Result result = await Sender.Send(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldCompleteRefund_WhenRefundProcessing()
    {
        // Arrange
        var setupResult = await BookingTestHelpers.SetupRefundProcessingBookingAsync(this);
        Guid bookingId = setupResult.bookingId;
        string guestToken = setupResult.guestToken;

        var command = new CompleteRefundCommand(bookingId, "refund_xyz", 100);

        // Act
        Result result = await Sender.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedBooking = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, guestToken);
        updatedBooking.Should().NotBeNull();
        updatedBooking.PaymentStatus.Should().Be((int)PaymentStatus.Refunded);
    }

    [Fact]
    public async Task Handle_ShouldBeIdempotent_WhenAlreadyRefunded()
    {
        // Arrange
        var setupResult = await BookingTestHelpers.SetupRefundProcessingBookingAsync(this);
        Guid bookingId = setupResult.bookingId;

        var command = new CompleteRefundCommand(bookingId, "refund_xyz", 100);

        // Act - 1st call
        Result result1 = await Sender.Send(command);
        result1.IsSuccess.Should().BeTrue();

        // Act - 2nd call
        Result result2 = await Sender.Send(command);

        // Assert
        result2.IsSuccess.Should().BeTrue();
    }
}
