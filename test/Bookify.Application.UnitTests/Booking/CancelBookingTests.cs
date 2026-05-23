using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Bookings.CancelBooking;
using Bookify.Application.Exceptions;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;

namespace Bookify.Application.UnitTests.Booking;

public class CancelBookingTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;
    
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly CancelBookingCommandHandler _handler;

    public CancelBookingTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new CancelBookingCommandHandler(
            _dateTimeProviderMock,
            _bookingRepositoryMock,
            _unitOfWorkMock);
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
    public async Task Handle_ShouldReturnFailure_WhenCancelFails()
    {
        // Arrange
        var command = new CancelBookingCommand(Guid.NewGuid());
        var booking = (Bookify.Domain.Bookings.Booking)Activator.CreateInstance(typeof(Bookify.Domain.Bookings.Booking), true)!;

        // Ensure status is NotConfirmed, specifically set it to Rejected
        typeof(Bookify.Domain.Bookings.Booking).GetProperty("Status")!.SetValue(booking, BookingStatus.Rejected);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotConfirmed);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenValid()
    {
        // Arrange
        var command = new CancelBookingCommand(Guid.NewGuid());

        var booking = (Bookify.Domain.Bookings.Booking)Activator.CreateInstance(typeof(Bookify.Domain.Bookings.Booking), true)!;

        // Ensure booking is in a valid state to be cancelled
        typeof(Bookify.Domain.Bookings.Booking).GetProperty("Status")!.SetValue(booking, BookingStatus.Confirmed);

        var duration = DateRange.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10));
        typeof(Bookify.Domain.Bookings.Booking).GetProperty("Duration")!.SetValue(booking, duration);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        DateTime cancellationTime = new DateTime(2024, 12, 31, 12, 0, 0, DateTimeKind.Utc);
        _dateTimeProviderMock.UtcNow.Returns(cancellationTime);

        // Act
        Result result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenUnitOfWorkThrows()
    {
        // Arrange
        var command = new CancelBookingCommand(Guid.NewGuid());

        var booking = (Bookify.Domain.Bookings.Booking)Activator.CreateInstance(typeof(Bookify.Domain.Bookings.Booking), true)!;

        // Ensure booking is in a valid state to be cancelled
        typeof(Bookify.Domain.Bookings.Booking).GetProperty("Status")!.SetValue(booking, BookingStatus.Confirmed);

        var duration = DateRange.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10));
        typeof(Bookify.Domain.Bookings.Booking).GetProperty("Duration")!.SetValue(booking, duration);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        DateTime cancellationTime = new DateTime(2024, 12, 31, 12, 0, 0, DateTimeKind.Utc);
        _dateTimeProviderMock.UtcNow.Returns(cancellationTime);

        _unitOfWorkMock.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyException("Concurrency", new InvalidOperationException()));

        // Act
        Func<Task> act = async () => await _handler.Handle(command, default);

        // Assert
        await act.Should().ThrowAsync<ConcurrencyException>();
    }
}
