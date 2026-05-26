using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Bookings.Events;

/// <summary>
/// Domain event raised when a booking is successfully checked in.
/// </summary>
public sealed record BookingCheckedInDomainEvent(Guid BookingId) : IDomainEvent;
