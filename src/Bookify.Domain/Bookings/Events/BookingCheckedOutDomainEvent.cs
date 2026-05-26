using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Bookings.Events;

/// <summary>
/// Domain event raised when a booking is successfully checked out.
/// </summary>
public sealed record BookingCheckedOutDomainEvent(Guid BookingId) : IDomainEvent;
