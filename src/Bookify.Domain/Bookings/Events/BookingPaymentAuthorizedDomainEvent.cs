using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Bookings.Events;

public sealed record BookingPaymentAuthorizedDomainEvent(
    Guid BookingId,
    string StripeSessionId,
    string? StripePaymentIntentId) : IDomainEvent;
