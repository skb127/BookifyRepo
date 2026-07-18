using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;

namespace Bookify.Application.Bookings.ReserveBooking;

public interface ITaxSnapshotService
{
    Task<IReadOnlyList<BookingTax>> CalculateAndSnapshotAsync(
        Booking booking,
        Apartment apartment,
        CancellationToken cancellationToken);
}
