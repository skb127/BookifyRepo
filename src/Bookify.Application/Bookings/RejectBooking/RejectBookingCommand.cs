using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Bookings;

namespace Bookify.Application.Bookings.RejectBooking;

public record RejectBookingCommand(
    Guid BookingId,
    ReasonType? ReasonType = null,
    string? ReasonDescription = null) : ICommand;
