using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Bookings.CloseStay;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Booking;

public class CloseStayCommandHandlerTests
{
    private static readonly DateTime UtcNow = new(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc);

    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly CloseStayCommandHandler _handler;

    public CloseStayCommandHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();

        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new CloseStayCommandHandler(
            _dateTimeProviderMock,
            _bookingRepositoryMock,
            _unitOfWorkMock);
    }

    private static Domain.Bookings.Booking CreateBooking(BookingStatus status, DateOnly start, DateOnly end)
    {
        var booking = (Domain.Bookings.Booking)Activator.CreateInstance(typeof(Domain.Bookings.Booking), true)!;
        typeof(Domain.Bookings.Booking).GetProperty(nameof(Domain.Bookings.Booking.Status))!.SetValue(booking, status);
        typeof(Domain.Bookings.Booking).GetProperty(nameof(Domain.Bookings.Booking.Duration))!.SetValue(booking, DateRange.Create(start, end));
        return booking;
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingNotFound()
    {
        // Arrange
        var command = new CloseStayCommand(Guid.NewGuid(), new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10));

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns((Domain.Bookings.Booking?)null);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenCloseStayFails()
    {
        // Arrange
        // Booking is in Reserved status (not Confirmed), so CloseStay will fail
        var booking = CreateBooking(BookingStatus.Reserved, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 4));
        var command = new CloseStayCommand(Guid.NewGuid(), new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 4));

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotConfirmed);
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenCloseStaySucceeds()
    {
        // Arrange
        // UtcNow is 2025-01-05. Booking is Confirmed and ends on 2025-01-04 (past).
        var booking = CreateBooking(BookingStatus.Confirmed, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 4));
        var command = new CloseStayCommand(Guid.NewGuid(), new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 4));

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Completed);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
