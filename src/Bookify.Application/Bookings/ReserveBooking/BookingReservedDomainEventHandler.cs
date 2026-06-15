using Bookify.Domain.Bookings.Events;
using MediatR;

namespace Bookify.Application.Bookings.ReserveBooking;

internal sealed class BookingReservedDomainEventHandler : INotificationHandler<BookingReservedDomainEvent>
{
    public Task Handle(BookingReservedDomainEvent notification, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
