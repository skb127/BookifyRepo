using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Bookings;

namespace Bookify.Application.Bookings.CancelBooking;

public record CancelBookingCommand(
    Guid BookingId,
    ReasonType? ReasonType = null,
    string? ReasonDescription = null) : ICommand;
