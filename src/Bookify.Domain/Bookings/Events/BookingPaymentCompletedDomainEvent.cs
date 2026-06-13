using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Bookings.Events;

public sealed record BookingPaymentCompletedDomainEvent(
    Guid BookingId,
    string StripePaymentIntentId) : IDomainEvent;
