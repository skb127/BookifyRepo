using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Payments;
using Bookify.Application.Bookings.ExpireHostApproval;
using Bookify.Application.UnitTests.Apartments;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Booking;

public class ExpireHostApprovalCommandHandlerTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly ITransactionRepository _transactionRepositoryMock;
    private readonly IPaymentGateway _paymentGatewayMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly ExpireHostApprovalCommandHandler _handler;

    public ExpireHostApprovalCommandHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _transactionRepositoryMock = Substitute.For<ITransactionRepository>();
        _paymentGatewayMock = Substitute.For<IPaymentGateway>();
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();

        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new ExpireHostApprovalCommandHandler(
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
        var command = new ExpireHostApprovalCommand(Guid.NewGuid());
        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns((Domain.Bookings.Booking?)null);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenBookingIsNotReserved()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)),
            UtcNow,
            new PricingService());

        var command = new ExpireHostApprovalCommand(booking.Id);
        _bookingRepositoryMock.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldExpireBookingAndCancelPaymentIntent_WhenBookingIsReservedAndAuthorized()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)),
            UtcNow,
            new PricingService());
        booking.AuthorizePayment("session_123", "pi_123");

        var transaction = Transaction.Create(
            booking.Id,
            "session_123",
            "pi_123",
            "https://checkout.stripe.com/test",
            booking.TotalPrice with { },
            "authorized",
            UtcNow);

        var command = new ExpireHostApprovalCommand(booking.Id);
        _bookingRepositoryMock.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(booking);
        _transactionRepositoryMock.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(transaction);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Expired);
        booking.PaymentStatus.Should().Be(PaymentStatus.AuthorizationReleased);

        await _paymentGatewayMock.Received(1).CancelPaymentIntentAsync("pi_123", Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
