using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Scheduling;
using Bookify.Application.Bookings.ReserveBooking;
using Bookify.Application.Options;
using Bookify.Application.UnitTests.Apartments;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Bookify.Application.UnitTests.Booking.Events;

public class BookingReservedDomainEventHandlerTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;
    private readonly BookingReservedDomainEventHandler _handler;

    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IJobScheduler _jobSchedulerMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IOptions<BookingOptions> _bookingOptionsMock;

    public BookingReservedDomainEventHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _jobSchedulerMock = Substitute.For<IJobScheduler>();
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _bookingOptionsMock = Substitute.For<IOptions<BookingOptions>>();

        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        var bookingOptions = new BookingOptions
        {
            CheckoutSessionTtlMinutes = 30.0,
            HostApprovalTtlHours = 24.0
        };
        _bookingOptionsMock.Value.Returns(bookingOptions);

        _handler = new BookingReservedDomainEventHandler(
            _bookingRepositoryMock,
            _jobSchedulerMock,
            _dateTimeProviderMock,
            _unitOfWorkMock,
            _bookingOptionsMock);
    }

    [Fact]
    public async Task Handle_ShouldScheduleExpireCheckoutSession_AndSaveExpiresAt()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(
            apartment,
            Guid.CreateVersion7(),
            DateRange.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10)),
            UtcNow,
            new PricingService());

        var bookingId = booking.Id;
        var domainEvent = new BookingReservedDomainEvent(bookingId);

        _bookingRepositoryMock
            .GetByIdAsync(bookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        var expectedExpiresAt = UtcNow.AddMinutes(30.0);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        booking.ExpiresAt.Should().BeCloseTo(expectedExpiresAt, TimeSpan.FromMilliseconds(100));

        await _jobSchedulerMock.Received(1).ScheduleExpireCheckoutSessionAsync(
            bookingId,
            Arg.Is<DateTime>(dt => Math.Abs((dt - expectedExpiresAt).TotalMilliseconds) < 100),
            Arg.Any<CancellationToken>());

        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
