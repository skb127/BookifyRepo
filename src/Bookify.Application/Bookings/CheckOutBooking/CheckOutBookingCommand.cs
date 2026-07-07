using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Bookings;

namespace Bookify.Application.Bookings.CheckOutBooking;

public sealed record CheckOutBookingCommand(
    Guid BookingId,
    ReasonType? ReasonType = null,
    string? ReasonDescription = null,
    DateOnly? GuestCheckOutDate = null) : ICommand;
