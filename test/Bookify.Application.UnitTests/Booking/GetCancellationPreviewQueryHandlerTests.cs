using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Bookings.GetCancellationPreview;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.CancellationPolicies;
using Bookify.Domain.Shared;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Bookify.Application.UnitTests.Booking;

public class GetCancellationPreviewQueryHandlerTests
{
    private static readonly DateTime UtcNow = new(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc); // 2 days before 2025-01-10

    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IApartmentRepository _apartmentRepositoryMock;
    private readonly ICancellationPolicyRepository _cancellationPolicyRepositoryMock;
    private readonly IUserContext _userContextMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly GetCancellationPreviewQueryHandler _handler;

    public GetCancellationPreviewQueryHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _apartmentRepositoryMock = Substitute.For<IApartmentRepository>();
        _cancellationPolicyRepositoryMock = Substitute.For<ICancellationPolicyRepository>();
        _userContextMock = Substitute.For<IUserContext>();
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();

        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new GetCancellationPreviewQueryHandler(
            _bookingRepositoryMock,
            _apartmentRepositoryMock,
            _cancellationPolicyRepositoryMock,
            _userContextMock,
            _dateTimeProviderMock);
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
            earlyHostPenaltyRate: 0.05m,
            lateHostPenaltyRate: 0.20m,
            thresholdHours: 72, // 3 days
            isDefault: true,
            utcNow: DateTime.UtcNow);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingNotFound()
    {
        // Arrange
        var query = new GetCancellationPreviewQuery(Guid.NewGuid());
        _bookingRepositoryMock.GetByIdAsync(query.BookingId, Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenApartmentNotFound()
    {
        // Arrange
        var booking = CreateBooking(BookingStatus.Confirmed, PaymentStatus.Paid, new Money(1000m, Currency.Usd), Guid.NewGuid());
        var query = new GetCancellationPreviewQuery(booking.Id);

        _bookingRepositoryMock.GetByIdAsync(query.BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ApartmentErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserUnauthorized()
    {
        // Arrange
        Guid guestId = Guid.NewGuid();
        Guid ownerId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();

        var booking = CreateBooking(BookingStatus.Confirmed, PaymentStatus.Paid, new Money(1000m, Currency.Usd), guestId);
        var apartment = CreateApartment(ownerId);
        var query = new GetCancellationPreviewQuery(booking.Id);

        _bookingRepositoryMock.GetByIdAsync(query.BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>()).Returns(apartment);
        _userContextMock.UserId.Returns(otherUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.Unauthorized);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingNotCancellable()
    {
        // Arrange
        Guid guestId = Guid.NewGuid();
        var booking = CreateBooking(BookingStatus.Completed, PaymentStatus.Paid, new Money(1000m, Currency.Usd), guestId);
        var apartment = CreateApartment(Guid.NewGuid());
        var query = new GetCancellationPreviewQuery(booking.Id);

        _bookingRepositoryMock.GetByIdAsync(query.BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>()).Returns(apartment);
        _userContextMock.UserId.Returns(guestId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotCancellable);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingAlreadyStarted()
    {
        // Arrange
        Guid guestId = Guid.NewGuid();
        var booking = CreateBooking(BookingStatus.Confirmed, PaymentStatus.Paid, new Money(1000m, Currency.Usd), guestId);
        var apartment = CreateApartment(Guid.NewGuid());
        var query = new GetCancellationPreviewQuery(booking.Id);

        _bookingRepositoryMock.GetByIdAsync(query.BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>()).Returns(apartment);
        _userContextMock.UserId.Returns(guestId);

        // Let's set UtcNow to after duration start (2025-01-10)
        _dateTimeProviderMock.UtcNow.Returns(new DateTime(2025, 1, 11, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.AlreadyStarted);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessWithZeroPenalty_WhenBookingNotPaid()
    {
        // Arrange
        Guid guestId = Guid.NewGuid();
        var booking = CreateBooking(BookingStatus.Reserved, PaymentStatus.Authorized, new Money(1000m, Currency.Usd), guestId);
        var apartment = CreateApartment(Guid.NewGuid());
        var query = new GetCancellationPreviewQuery(booking.Id);

        _bookingRepositoryMock.GetByIdAsync(query.BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>()).Returns(apartment);
        _userContextMock.UserId.Returns(guestId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPrice.Should().Be(1000m);
        result.Value.PenaltyAmount.Should().Be(0m);
        result.Value.RefundAmount.Should().Be(0m);
        result.Value.RequiresRefund.Should().BeFalse();
        result.Value.CancellationPolicyName.Should().Be("None");
        result.Value.IsCancelledByHost.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenNoPolicyAvailable()
    {
        // Arrange
        Guid guestId = Guid.NewGuid();
        var booking = CreateBooking(BookingStatus.Confirmed, PaymentStatus.Paid, new Money(1000m, Currency.Usd), guestId);
        var apartment = CreateApartment(Guid.NewGuid());
        var query = new GetCancellationPreviewQuery(booking.Id);

        _bookingRepositoryMock.GetByIdAsync(query.BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>()).Returns(apartment);
        _userContextMock.UserId.Returns(guestId);
        _cancellationPolicyRepositoryMock.GetDefaultAsync(Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NoPolicyAvailable);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessWithDetails_WhenGuestCancelsEarly()
    {
        // Arrange
        Guid guestId = Guid.NewGuid();
        var booking = CreateBooking(BookingStatus.Confirmed, PaymentStatus.Paid, new Money(1000m, Currency.Usd), guestId);
        var apartment = CreateApartment(Guid.NewGuid());
        var policy = CreateCancellationPolicy();
        var query = new GetCancellationPreviewQuery(booking.Id);

        _bookingRepositoryMock.GetByIdAsync(query.BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>()).Returns(apartment);
        _userContextMock.UserId.Returns(guestId);
        _cancellationPolicyRepositoryMock.GetDefaultAsync(Arg.Any<CancellationToken>()).Returns(policy);

        // Let's set UtcNow to early: check-in is 2025-01-10, let's cancel at 2025-01-05 (5 days/120 hours before, > 72 threshold)
        _dateTimeProviderMock.UtcNow.Returns(new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPrice.Should().Be(1000m);
        // EarlyGuestPenaltyRate is 10% -> 100m
        result.Value.PenaltyAmount.Should().Be(100m);
        result.Value.RefundAmount.Should().Be(900m);
        result.Value.RequiresRefund.Should().BeTrue();
        result.Value.IsLateCancellation.Should().BeFalse();
        result.Value.CancellationPolicyName.Should().Be("Standard Policy");
        result.Value.IsCancelledByHost.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessWithDetails_WhenGuestCancelsLate()
    {
        // Arrange
        Guid guestId = Guid.NewGuid();
        var booking = CreateBooking(BookingStatus.Confirmed, PaymentStatus.Paid, new Money(1000m, Currency.Usd), guestId);
        var apartment = CreateApartment(Guid.NewGuid());
        var policy = CreateCancellationPolicy();
        var query = new GetCancellationPreviewQuery(booking.Id);

        _bookingRepositoryMock.GetByIdAsync(query.BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>()).Returns(apartment);
        _userContextMock.UserId.Returns(guestId);
        _cancellationPolicyRepositoryMock.GetDefaultAsync(Arg.Any<CancellationToken>()).Returns(policy);

        // Let's set UtcNow to late: check-in is 2025-01-10, let's cancel at 2025-01-08 (2 days/48 hours before, < 72 threshold)
        _dateTimeProviderMock.UtcNow.Returns(new DateTime(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPrice.Should().Be(1000m);
        // LateGuestPenaltyRate is 50% -> 500m
        result.Value.PenaltyAmount.Should().Be(500m);
        result.Value.RefundAmount.Should().Be(500m);
        result.Value.RequiresRefund.Should().BeTrue();
        result.Value.IsLateCancellation.Should().BeTrue();
        result.Value.CancellationPolicyName.Should().Be("Standard Policy");
        result.Value.IsCancelledByHost.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessWithDetails_WhenHostCancelsEarly()
    {
        // Arrange
        Guid guestId = Guid.NewGuid();
        Guid ownerId = Guid.NewGuid();
        var booking = CreateBooking(BookingStatus.Confirmed, PaymentStatus.Paid, new Money(1000m, Currency.Usd), guestId);
        var apartment = CreateApartment(ownerId);
        var policy = CreateCancellationPolicy();
        var query = new GetCancellationPreviewQuery(booking.Id);

        _bookingRepositoryMock.GetByIdAsync(query.BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>()).Returns(apartment);
        _userContextMock.UserId.Returns(ownerId);
        _cancellationPolicyRepositoryMock.GetDefaultAsync(Arg.Any<CancellationToken>()).Returns(policy);

        // Early: 5 days before
        _dateTimeProviderMock.UtcNow.Returns(new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPrice.Should().Be(1000m);
        // EarlyHostPenaltyRate is 5% -> 50m
        result.Value.PenaltyAmount.Should().Be(50m);
        // For host cancel, refund to guest is 100% (1000m)
        result.Value.RefundAmount.Should().Be(1000m);
        result.Value.RequiresRefund.Should().BeTrue();
        result.Value.IsLateCancellation.Should().BeFalse();
        result.Value.CancellationPolicyName.Should().Be("Standard Policy");
        result.Value.IsCancelledByHost.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessWithDetails_WhenHostCancelsLate()
    {
        // Arrange
        Guid guestId = Guid.NewGuid();
        Guid ownerId = Guid.NewGuid();
        var booking = CreateBooking(BookingStatus.Confirmed, PaymentStatus.Paid, new Money(1000m, Currency.Usd), guestId);
        var apartment = CreateApartment(ownerId);
        var policy = CreateCancellationPolicy();
        var query = new GetCancellationPreviewQuery(booking.Id);

        _bookingRepositoryMock.GetByIdAsync(query.BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>()).Returns(apartment);
        _userContextMock.UserId.Returns(ownerId);
        _cancellationPolicyRepositoryMock.GetDefaultAsync(Arg.Any<CancellationToken>()).Returns(policy);

        // Late: 2 days before
        _dateTimeProviderMock.UtcNow.Returns(new DateTime(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPrice.Should().Be(1000m);
        // LateHostPenaltyRate is 20% -> 200m
        result.Value.PenaltyAmount.Should().Be(200m);
        // For host cancel, refund to guest is 100% (1000m)
        result.Value.RefundAmount.Should().Be(1000m);
        result.Value.RequiresRefund.Should().BeTrue();
        result.Value.IsLateCancellation.Should().BeTrue();
        result.Value.CancellationPolicyName.Should().Be("Standard Policy");
        result.Value.IsCancelledByHost.Should().BeTrue();
    }
}
