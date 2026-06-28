using System.Data;
using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Scheduling;
using Bookify.Application.Bookings.CancelBooking;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.CancellationPolicies;
using Bookify.Domain.Shared;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Bookify.Application.UnitTests.Booking;

public class CancelBookingCommandHandlerTests
{
    private static readonly DateTime UtcNow = new(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc); // 2 days before 2025-01-10

    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly ICancellationPolicyRepository _cancellationPolicyRepositoryMock;
    private readonly IApartmentRepository _apartmentRepositoryMock;
    private readonly ISqlConnectionFactory _sqlConnectionFactoryMock;
    private readonly IJobScheduler _jobSchedulerMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IUserContext _userContextMock;
    private readonly CancelBookingCommandHandler _handler;

    public CancelBookingCommandHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _cancellationPolicyRepositoryMock = Substitute.For<ICancellationPolicyRepository>();
        _apartmentRepositoryMock = Substitute.For<IApartmentRepository>();
        _sqlConnectionFactoryMock = Substitute.For<ISqlConnectionFactory>();
        _jobSchedulerMock = Substitute.For<IJobScheduler>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _userContextMock = Substitute.For<IUserContext>();

        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new CancelBookingCommandHandler(
            _dateTimeProviderMock,
            _bookingRepositoryMock,
            _cancellationPolicyRepositoryMock,
            _apartmentRepositoryMock,
            _sqlConnectionFactoryMock,
            _jobSchedulerMock,
            _unitOfWorkMock,
            _userContextMock);

        SetupMockConnection();
    }

    private void SetupMockConnection()
    {
        var connectionMock = Substitute.For<IDbConnection>();
        var commandMock = Substitute.For<System.Data.Common.DbCommand>();
        var parameterMock = Substitute.For<System.Data.Common.DbParameter>();
        var parametersMock = Substitute.For<System.Data.Common.DbParameterCollection>();

        connectionMock.State.Returns(ConnectionState.Open);
        connectionMock.CreateCommand().Returns(commandMock);
        commandMock.Parameters.Returns(parametersMock);
        commandMock.CreateParameter().Returns(parameterMock);

        _sqlConnectionFactoryMock.CreateConnection().Returns(connectionMock);
    }

    private static Bookify.Domain.Bookings.Booking CreateBooking(BookingStatus status, PaymentStatus paymentStatus, Money price, Guid userId)
    {
        var booking = (Bookify.Domain.Bookings.Booking)Activator.CreateInstance(typeof(Bookify.Domain.Bookings.Booking), true)!;
        typeof(Bookify.Domain.Bookings.Booking).GetProperty("Status")!.SetValue(booking, status);
        typeof(Bookify.Domain.Bookings.Booking).GetProperty("PaymentStatus")!.SetValue(booking, paymentStatus);
        typeof(Bookify.Domain.Bookings.Booking).GetProperty("TotalPrice")!.SetValue(booking, price);
        typeof(Bookify.Domain.Bookings.Booking).GetProperty("UserId")!.SetValue(booking, userId);

        var duration = DateRange.Create(new DateOnly(2025, 1, 10), new DateOnly(2025, 1, 15));
        typeof(Bookify.Domain.Bookings.Booking).GetProperty("Duration")!.SetValue(booking, duration);
        typeof(Bookify.Domain.Bookings.Booking).GetProperty("ApartmentId")!.SetValue(booking, Guid.NewGuid());

        return booking;
    }

    private static Apartment CreateApartment(Guid ownerId)
    {
        var apartment = (Apartment)Activator.CreateInstance(typeof(Apartment), true)!;
        typeof(Apartment).GetProperty("OwnerId")!.SetValue(apartment, ownerId);
        return apartment;
    }

    private static CancellationPolicy CreateCancellationPolicy()
    {
        return CancellationPolicy.Create(
            "Standard Policy",
            earlyGuestPenaltyRate: 0.10m, // 10% penalty
            lateGuestPenaltyRate: 0.50m,
            earlyHostPenaltyRate: 0.20m,
            lateHostPenaltyRate: 0.80m,
            thresholdHours: 24,
            isDefault: true,
            utcNow: UtcNow);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingNotFound()
    {
        // Arrange
        var command = new CancelBookingCommand(Guid.NewGuid());

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        Result result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenApartmentNotFound()
    {
        // Arrange
        var guestId = Guid.NewGuid();
        var command = new CancelBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.PendingPayment, PaymentStatus.Unpaid, new Money(100.00m, Currency.Usd), guestId);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        Result result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ApartmentErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserIsUnauthorized()
    {
        // Arrange
        var guestId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var command = new CancelBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.Confirmed, PaymentStatus.Paid, new Money(100.00m, Currency.Usd), guestId);
        var apartment = CreateApartment(hostId);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _userContextMock.UserId.Returns(strangerId); // Someone else

        // Act
        Result result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.Unauthorized);
    }

    [Fact]
    public async Task Handle_ShouldSucceedAndCancelTTL1_WhenBookingIsUnpaid()
    {
        // Arrange
        var guestId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var command = new CancelBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.PendingPayment, PaymentStatus.Unpaid, new Money(100.00m, Currency.Usd), guestId);
        var apartment = CreateApartment(hostId);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _userContextMock.UserId.Returns(guestId); // Guest is cancelling

        // Act
        Result result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        await _jobSchedulerMock.Received(1).CancelExpireCheckoutSessionAsync(booking.Id, Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenHostCancelsBookingInPendingPaymentStatus()
    {
        // Arrange
        var guestId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var command = new CancelBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.PendingPayment, PaymentStatus.Unpaid, new Money(100.00m, Currency.Usd), guestId);
        var apartment = CreateApartment(hostId);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _userContextMock.UserId.Returns(hostId); // Host is cancelling

        // Act
        Result result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotCancellable);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenNoPolicyAvailable()
    {
        // Arrange
        var guestId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var command = new CancelBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.Confirmed, PaymentStatus.Paid, new Money(100.00m, Currency.Usd), guestId);
        var apartment = CreateApartment(hostId);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _userContextMock.UserId.Returns(guestId);

        _cancellationPolicyRepositoryMock.GetDefaultAsync(Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        Result result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NoPolicyAvailable);
    }

    [Fact]
    public async Task Handle_ShouldSucceedWithGuestEarlyCancellation_WhenGuestCancelsEarly()
    {
        // Arrange
        var guestId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var command = new CancelBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.Confirmed, PaymentStatus.Paid, new Money(100.00m, Currency.Usd), guestId);
        var policy = CreateCancellationPolicy();
        var apartment = CreateApartment(hostId);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _userContextMock.UserId.Returns(guestId); // Guest is cancelling

        _cancellationPolicyRepositoryMock.GetDefaultAsync(Arg.Any<CancellationToken>())
            .Returns(policy);

        // Act
        Result result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceedWithGuestLateCancellation_WhenGuestCancelsLate()
    {
        // Arrange
        var guestId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var command = new CancelBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.Confirmed, PaymentStatus.Paid, new Money(100.00m, Currency.Usd), guestId);
        var policy = CreateCancellationPolicy();
        var apartment = CreateApartment(hostId);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _userContextMock.UserId.Returns(guestId); // Guest is cancelling

        _cancellationPolicyRepositoryMock.GetDefaultAsync(Arg.Any<CancellationToken>())
            .Returns(policy);

        // Mock UtcNow to be 12 hours before check-in (cancellation is late)
        // Check-in starts on 2025-01-10 00:00:00. 12 hours before is 2025-01-09 12:00:00.
        var lateUtcNow = new DateTime(2025, 1, 9, 12, 0, 0, DateTimeKind.Utc);
        _dateTimeProviderMock.UtcNow.Returns(lateUtcNow);

        // Act
        Result result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceedWithHostEarlyCancellation_WhenHostCancelsEarly()
    {
        // Arrange
        var guestId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var command = new CancelBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.Confirmed, PaymentStatus.Paid, new Money(100.00m, Currency.Usd), guestId);
        var policy = CreateCancellationPolicy();
        var apartment = CreateApartment(hostId);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _userContextMock.UserId.Returns(hostId); // Host is cancelling

        _cancellationPolicyRepositoryMock.GetDefaultAsync(Arg.Any<CancellationToken>())
            .Returns(policy);

        // Act
        Result result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceedWithHostLateCancellation_WhenHostCancelsLate()
    {
        // Arrange
        var guestId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var command = new CancelBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.Confirmed, PaymentStatus.Paid, new Money(100.00m, Currency.Usd), guestId);
        var policy = CreateCancellationPolicy();
        var apartment = CreateApartment(hostId);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _userContextMock.UserId.Returns(hostId); // Host is cancelling

        _cancellationPolicyRepositoryMock.GetDefaultAsync(Arg.Any<CancellationToken>())
            .Returns(policy);

        // Mock UtcNow to be 12 hours before check-in (cancellation is late)
        var lateUtcNow = new DateTime(2025, 1, 9, 12, 0, 0, DateTimeKind.Utc);
        _dateTimeProviderMock.UtcNow.Returns(lateUtcNow);

        // Act
        Result result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceedWithoutPenalty_WhenBookingIsAuthorized()
    {
        // Arrange
        var guestId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var command = new CancelBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.Reserved, PaymentStatus.Authorized, new Money(100.00m, Currency.Usd), guestId);
        var apartment = CreateApartment(hostId);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _userContextMock.UserId.Returns(guestId);

        // Act
        Result result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.PaymentStatus.Should().Be(PaymentStatus.AuthorizationReleased);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
