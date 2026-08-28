using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Payments.FailRefund;
using Bookify.Application.UnitTests.Apartments;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.CancellationPolicies;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using BookingEntity = Bookify.Domain.Bookings.Booking;

namespace Bookify.Application.UnitTests.Payments;

public class FailRefundCommandHandlerTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly ILogger<FailRefundCommandHandler> _loggerMock;
    private readonly FailRefundCommandHandler _handler;

    public FailRefundCommandHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _loggerMock = Substitute.For<ILogger<FailRefundCommandHandler>>();

        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new FailRefundCommandHandler(
            _bookingRepositoryMock,
            _dateTimeProviderMock,
            _unitOfWorkMock,
            _loggerMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingNotFound()
    {
        // Arrange
        var command = new FailRefundCommand(Guid.NewGuid(), "re_123", "card_declined");
        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns((BookingEntity?)null);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenBookingPaymentStatusIsNotRefundProcessing()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        BookingEntity booking = BookingEntity.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)),
            UtcNow,
            new PricingService());
        booking.MarkAsPaid("pi_123", UtcNow);

        var command = new FailRefundCommand(booking.Id, "re_123", "card_declined");
        _bookingRepositoryMock.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRevertToPaid_WhenBookingPaymentStatusIsRefundProcessing()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        BookingEntity booking = BookingEntity.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)),
            UtcNow,
            new PricingService());
        booking.MarkAsPaid("pi_123", UtcNow);
        var policy = CancellationPolicy.Create("Policy", 0m, 0.5m, 0.1m, 1m, 24, true, UtcNow);
        var engine = new CancellationPolicyEngine();
        booking.Cancel(UtcNow, policy, engine, false);
        booking.InitiateRefund(100m, "USD", "Cancellation refund", UtcNow);

        var command = new FailRefundCommand(booking.Id, "re_123", "insufficient_funds");
        _bookingRepositoryMock.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.PaymentStatus.Should().Be(PaymentStatus.Paid);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
