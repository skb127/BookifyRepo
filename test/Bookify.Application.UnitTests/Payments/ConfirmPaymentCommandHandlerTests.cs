using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Scheduling;
using Bookify.Application.Payments.ConfirmPayment;
using Bookify.Application.UnitTests.Apartments;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using FluentAssertions;
using NSubstitute;
using BookingEntity = Bookify.Domain.Bookings.Booking;

namespace Bookify.Application.UnitTests.Payments;

public class ConfirmPaymentCommandHandlerTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly ITransactionRepository _transactionRepositoryMock;
    private readonly IJobScheduler _jobSchedulerMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly ConfirmPaymentCommandHandler _handler;

    public ConfirmPaymentCommandHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _transactionRepositoryMock = Substitute.For<ITransactionRepository>();
        _jobSchedulerMock = Substitute.For<IJobScheduler>();
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();

        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new ConfirmPaymentCommandHandler(
            _bookingRepositoryMock,
            _transactionRepositoryMock,
            _jobSchedulerMock,
            _dateTimeProviderMock,
            _unitOfWorkMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingNotFound()
    {
        // Arrange
        var command = new ConfirmPaymentCommand(Guid.NewGuid(), "session_123", "pi_123", true);
        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns((BookingEntity?)null);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenBookingAlreadyConfirmed()
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

        var command = new ConfirmPaymentCommand(booking.Id, "session_123", "pi_123", true);
        _bookingRepositoryMock.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAuthorizePaymentAndCreateTransaction_WhenNotInstantBooking()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        BookingEntity booking = BookingEntity.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)),
            UtcNow,
            new PricingService());

        var command = new ConfirmPaymentCommand(booking.Id, "session_123", "pi_123", false);
        _bookingRepositoryMock.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(booking);
        _transactionRepositoryMock.GetByStripeSessionIdAsync("session_123", Arg.Any<CancellationToken>())
            .Returns((Transaction?)null);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Reserved);
        booking.PaymentStatus.Should().Be(PaymentStatus.Authorized);

        await _jobSchedulerMock.Received(1).CancelExpireCheckoutSessionAsync(booking.Id, Arg.Any<CancellationToken>());
        _transactionRepositoryMock.Received(1).Add(Arg.Is<Transaction>(t =>
            t.BookingId == booking.Id &&
            t.StripeSessionId == "session_123" &&
            t.ProviderStatus == "authorized"));
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldMarkAsPaidAndUpdateTransaction_WhenInstantBooking()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        BookingEntity booking = BookingEntity.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)),
            UtcNow,
            new PricingService());

        var existingTransaction = Transaction.Create(
            booking.Id,
            "session_123",
            null,
            "https://checkout.stripe.com/test",
            booking.TotalPrice with { },
            "pending",
            UtcNow);

        var command = new ConfirmPaymentCommand(booking.Id, "session_123", "pi_123", true);
        _bookingRepositoryMock.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(booking);
        _transactionRepositoryMock.GetByStripeSessionIdAsync("session_123", Arg.Any<CancellationToken>())
            .Returns(existingTransaction);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.PaymentStatus.Should().Be(PaymentStatus.Paid);

        existingTransaction.ProviderStatus.Should().Be("paid");
        existingTransaction.StripePaymentIntentId.Should().Be("pi_123");

        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
