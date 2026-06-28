using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Bookings.CompleteBooking;
using Bookify.Application.Exceptions;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;

namespace Bookify.Application.UnitTests.Booking;

public class CompleteBookingCommandHandlerTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;

    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly CompleteBookingCommandHandler _handler;

    public CompleteBookingCommandHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();

        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new CompleteBookingCommandHandler(
            _dateTimeProviderMock,
            _bookingRepositoryMock,
            _unitOfWorkMock);
    }

    private static Domain.Bookings.Booking CreateBooking(BookingStatus status)
    {
        var booking = (Domain.Bookings.Booking)Activator.CreateInstance(typeof(Domain.Bookings.Booking), true)!;
        typeof(Domain.Bookings.Booking).GetProperty(nameof(Domain.Bookings.Booking.Status))!.SetValue(booking, status);
        return booking;
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingNotFound()
    {
        // Arrange
        var command = new CompleteBookingCommand(Guid.NewGuid());

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingIsNotInProgress()
    {
        // Arrange
        var command = new CompleteBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.Reserved); // Status is Reserved, not InProgress

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotInProgress);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenValid()
    {
        // Arrange
        var command = new CompleteBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.InProgress);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenUnitOfWorkThrows()
    {
        // Arrange
        var command = new CompleteBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.InProgress);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _dateTimeProviderMock.UtcNow.Returns(DateTime.UtcNow);

        _unitOfWorkMock.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyException("Concurrency", new InvalidOperationException()));

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConcurrencyException>();
    }
}
