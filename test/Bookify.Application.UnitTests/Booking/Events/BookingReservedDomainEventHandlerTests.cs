using Bookify.Application.Bookings.ReserveBooking;
using Bookify.Domain.Bookings.Events;
using FluentAssertions;

namespace Bookify.Application.UnitTests.Booking.Events;

public class BookingReservedDomainEventHandlerTests
{
    private readonly BookingReservedDomainEventHandler _handler;

    public BookingReservedDomainEventHandlerTests()
    {
        _handler = new BookingReservedDomainEventHandler();
    }

    [Fact]
    public async Task Handle_ShouldCompleteSuccessfully_WithoutInvokingServices()
    {
        // Arrange
        var domainEvent = new BookingReservedDomainEvent(Guid.CreateVersion7());

        // Act
        Func<Task> act = async () => await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
