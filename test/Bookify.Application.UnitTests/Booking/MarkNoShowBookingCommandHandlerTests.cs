using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Bookings.MarkNoShowBooking;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Bookify.Application.UnitTests.Booking;

public class MarkNoShowBookingCommandHandlerTests
{
    private static readonly DateTime UtcNow = new(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);

    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly MarkNoShowBookingCommandHandler _handler;

    public MarkNoShowBookingCommandHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();

        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new MarkNoShowBookingCommandHandler(
            _dateTimeProviderMock,
            _bookingRepositoryMock,
            _unitOfWorkMock);
    }

    private static Domain.Bookings.Booking CreateBooking(BookingStatus status, DateRange duration)
    {
        var booking = (Domain.Bookings.Booking)Activator.CreateInstance(typeof(Domain.Bookings.Booking), true)!;
        typeof(Domain.Bookings.Booking).GetProperty(nameof(Domain.Bookings.Booking.Status))!.SetValue(booking, status);
        typeof(Domain.Bookings.Booking).GetProperty(nameof(Domain.Bookings.Booking.Duration))!.SetValue(booking,
            duration);
        return booking;
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingNotFound()
    {
        // Arrange
        var command = new MarkNoShowBookingCommand(Guid.NewGuid());

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingIsNotConfirmed()
    {
        // Arrange
        var command = new MarkNoShowBookingCommand(Guid.NewGuid());
        var duration = DateRange.Create(DateOnly.FromDateTime(UtcNow).AddDays(-1),
            DateOnly.FromDateTime(UtcNow).AddDays(2));
        var booking = CreateBooking(BookingStatus.Reserved, duration);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotConfirmed);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenCurrentDateIsBeforeOrOnStartDate()
    {
        // Arrange
        var command = new MarkNoShowBookingCommand(Guid.NewGuid());
        // Start date is today (same as UtcNow.Date), which violates: DateOnly.FromDateTime(utcNow) > Duration.Start
        var duration = DateRange.Create(DateOnly.FromDateTime(UtcNow), DateOnly.FromDateTime(UtcNow).AddDays(2));
        var booking = CreateBooking(BookingStatus.Confirmed, duration);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.CannotMarkNoShow);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenValid()
    {
        // Arrange
        var command = new MarkNoShowBookingCommand(Guid.NewGuid());
        // Start date is yesterday, which satisfies: DateOnly.FromDateTime(utcNow) > Duration.Start
        var duration = DateRange.Create(DateOnly.FromDateTime(UtcNow).AddDays(-1),
            DateOnly.FromDateTime(UtcNow).AddDays(2));
        var booking = CreateBooking(BookingStatus.Confirmed, duration);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.NoShow);
        booking.NoShowAt.Should().Be(UtcNow);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
