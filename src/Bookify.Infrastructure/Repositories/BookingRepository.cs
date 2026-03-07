using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Infrastructure.Repositories;

internal sealed class BookingRepository : Repository<Booking>, IBookingRepository
{
    private static readonly BookingStatus[] ActiveBookingStatuses = [
        BookingStatus.Reserved,
        BookingStatus.Confirmed,
        BookingStatus.Completed
    ];

    private static readonly BookingStatus[] BlockingDeleteBookingStatuses = [
        BookingStatus.Reserved,
        BookingStatus.Confirmed
    ];

    public BookingRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<bool> IsOverlappingAsync(Apartment apartment, DateRange duration,
        CancellationToken cancellationToken = default) =>
        await DbContext
            .Set<Booking>()
            .AnyAsync(
                booking =>
                    booking.ApartmentId == apartment.Id &&
                    booking.Duration.Start <= duration.End &&
                    booking.Duration.End >= duration.Start &&
                    ActiveBookingStatuses.Contains(booking.Status),
            cancellationToken);

    public async Task<bool> HasActiveBookingsAsync(Guid apartmentId, CancellationToken cancellationToken = default) =>
        await DbContext
            .Set<Booking>()
            .AnyAsync(
                booking =>
                    booking.ApartmentId == apartmentId &&
                    BlockingDeleteBookingStatuses.Contains(booking.Status),
                cancellationToken);
}
