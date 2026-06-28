using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Bookings.Events;

public sealed record BookingCancelledDomainEvent(
    Guid BookingId,
    decimal? RefundAmount = null,
    bool CancelledByHost = false,
    string? Currency = null,
    decimal HostPenaltyAmount = 0) : IDomainEvent;
