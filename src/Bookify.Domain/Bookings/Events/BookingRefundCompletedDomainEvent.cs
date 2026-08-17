using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Bookings.Events;

public sealed record BookingRefundCompletedDomainEvent(Guid BookingId) : IDomainEvent;
