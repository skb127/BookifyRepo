using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Bookings.ReserveBooking;

public record ReserveBookingCommand(
    Guid ApartmentId,
    DateOnly StartDate,
    DateOnly EndDate,
    int GuestCount = 1) : ICommand<Guid>;
