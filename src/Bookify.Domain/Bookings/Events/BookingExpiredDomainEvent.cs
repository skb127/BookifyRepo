using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Bookings.Events;

/// <summary>
/// Domain event raised when a booking expires due to non-payment within the courtesy block period.
/// </summary>
public sealed record BookingExpiredDomainEvent(Guid BookingId) : IDomainEvent;
