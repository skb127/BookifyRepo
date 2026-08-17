using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Payments.CompleteRefund;
using Bookify.Application.UnitTests.Apartments;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.CancellationPolicies;
using FluentAssertions;
using NSubstitute;
using BookingEntity = Bookify.Domain.Bookings.Booking;

namespace Bookify.Application.UnitTests.Payments;

public class CompleteRefundCommandHandlerTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly CompleteRefundCommandHandler _handler;

    public CompleteRefundCommandHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();

        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new CompleteRefundCommandHandler(
            _bookingRepositoryMock,
            _dateTimeProviderMock,
            _unitOfWorkMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingNotFound()
    {
        // Arrange
        var command = new CompleteRefundCommand(Guid.NewGuid(), "re_123", 100m);
        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns((BookingEntity?)null);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenBookingAlreadyRefunded()
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
        booking.InitiateRefund(100m, "USD", "Cancellation refund");
        booking.CompleteRefund(UtcNow);

        var command = new CompleteRefundCommand(booking.Id, "re_123", 100m);
        _bookingRepositoryMock.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCompleteRefund_WhenPaymentStatusIsRefundProcessing()
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
        booking.InitiateRefund(100m, "USD", "Cancellation refund");

        var command = new CompleteRefundCommand(booking.Id, "re_123", 100m);
        _bookingRepositoryMock.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.PaymentStatus.Should().Be(PaymentStatus.Refunded);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingIsNotInRefundProcessing()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        BookingEntity booking = BookingEntity.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)),
            UtcNow,
            new PricingService());

        var command = new CompleteRefundCommand(booking.Id, "re_123", 100m);
        _bookingRepositoryMock.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.RefundNotEligible);
    }
}
