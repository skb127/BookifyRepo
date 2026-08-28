using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Payments;
using Bookify.Application.Bookings.ExpireCheckoutSession;
using Bookify.Application.UnitTests.Apartments;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Booking;

public class ExpireCheckoutSessionCommandHandlerTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly ITransactionRepository _transactionRepositoryMock;
    private readonly IPaymentGateway _paymentGatewayMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly ExpireCheckoutSessionCommandHandler _handler;

    public ExpireCheckoutSessionCommandHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _transactionRepositoryMock = Substitute.For<ITransactionRepository>();
        _paymentGatewayMock = Substitute.For<IPaymentGateway>();
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();

        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new ExpireCheckoutSessionCommandHandler(
            _bookingRepositoryMock,
            _transactionRepositoryMock,
            _paymentGatewayMock,
            _dateTimeProviderMock,
            _unitOfWorkMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingNotFound()
    {
        // Arrange
        var command = new ExpireCheckoutSessionCommand(Guid.NewGuid());
        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns((Domain.Bookings.Booking?)null);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenBookingIsNotPendingPayment()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)),
            UtcNow,
            new PricingService());
        booking.MarkAsPaid("pi_123", UtcNow);

        var command = new ExpireCheckoutSessionCommand(booking.Id);
        _bookingRepositoryMock.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldExpireBooking_WhenBookingIsPendingPayment()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)),
            UtcNow,
            new PricingService());

        var command = new ExpireCheckoutSessionCommand(booking.Id);
        _bookingRepositoryMock.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Expired);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
