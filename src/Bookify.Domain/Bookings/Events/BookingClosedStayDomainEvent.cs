using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Bookings.Events;

public sealed record BookingClosedStayDomainEvent(Guid BookingId) : IDomainEvent;
