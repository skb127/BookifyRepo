using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Bookings.Events;

/// <summary>
/// Domain event raised when a guest fails to show up for a confirmed booking.
/// </summary>
public sealed record BookingNoShowDomainEvent(Guid BookingId) : IDomainEvent;
