using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Scheduling;
using Bookify.Application.Bookings.ConfirmBooking;
using Bookify.Application.UnitTests.Apartments;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Booking;

public class ConfirmBookingCommandHandlerTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IJobScheduler _jobSchedulerMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly ConfirmBookingCommandHandler _handler;

    public ConfirmBookingCommandHandlerTests()
    {
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _jobSchedulerMock = Substitute.For<IJobScheduler>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();

        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new ConfirmBookingCommandHandler(
            _dateTimeProviderMock,
            _bookingRepositoryMock,
            _jobSchedulerMock,
            _unitOfWorkMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingNotFound()
    {
        // Arrange
        var command = new ConfirmBookingCommand(Guid.NewGuid());
        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns((Domain.Bookings.Booking?)null);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingIsNotReserved()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)),
            UtcNow,
            new PricingService());

        var command = new ConfirmBookingCommand(booking.Id);
        _bookingRepositoryMock.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotReserved);
    }

    [Fact]
    public async Task Handle_ShouldConfirmBooking_WhenStatusIsReserved()
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

        var command = new ConfirmBookingCommand(booking.Id);
        _bookingRepositoryMock.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.PaymentStatus.Should().Be(PaymentStatus.Paid);

        await _jobSchedulerMock.Received(1).CancelExpireHostApprovalAsync(booking.Id, Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
