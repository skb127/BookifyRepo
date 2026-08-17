using Bookify.Domain.Apartments;

namespace Bookify.Domain.Bookings;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Booking?> GetWithTaxesAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> IsOverlappingAsync(
        Apartment apartment,
        DateRange duration,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveBookingsAsync(Guid apartmentId, CancellationToken cancellationToken = default);

    Task<bool> HasActiveBookingsAsGuestAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> HasActiveBookingsAsHostAsync(Guid hostId, CancellationToken cancellationToken = default);

    void Add(Booking booking);
}
