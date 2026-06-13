using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Bookings.Events;

public sealed record BookingRefundInitiatedDomainEvent(
    Guid BookingId,
    decimal RefundAmount,
    string Currency,
    string Reason) : IDomainEvent;
