using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Bookings.CheckInBooking;

public sealed record CheckInBookingCommand(
    Guid BookingId,
    DateOnly? GuestCheckInDate = null) : ICommand;
