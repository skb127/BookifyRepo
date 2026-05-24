using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Bookings.MarkNoShowBooking;

public sealed record MarkNoShowBookingCommand(Guid BookingId) : ICommand;
