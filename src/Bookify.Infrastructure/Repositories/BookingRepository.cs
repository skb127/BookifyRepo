using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Infrastructure.Repositories;

internal sealed class BookingRepository : Repository<Booking>, IBookingRepository
{

    private readonly Bookify.Application.Abstractions.Clock.IDateTimeProvider _dateTimeProvider;

    public BookingRepository(
        ApplicationDbContext dbContext,
        Bookify.Application.Abstractions.Clock.IDateTimeProvider dateTimeProvider)
        : base(dbContext) =>
        _dateTimeProvider = dateTimeProvider;

    public async Task<bool> IsOverlappingAsync(Apartment apartment, DateRange duration,
        CancellationToken cancellationToken = default) =>
        await DbContext
            .Set<Booking>()
            .AnyAsync(
                booking =>
                    booking.ApartmentId == apartment.Id &&
                    booking.Duration.Start <= duration.End &&
                    booking.Duration.End >= duration.Start &&
                    (booking.Status == BookingStatus.Confirmed ||
                     booking.Status == BookingStatus.InProgress ||
                     booking.Status == BookingStatus.Reserved && booking.ExpiresAt > _dateTimeProvider.UtcNow),
            cancellationToken);

    public async Task<bool> HasActiveBookingsAsync(Guid apartmentId, CancellationToken cancellationToken = default) =>
        await DbContext
            .Set<Booking>()
            .AnyAsync(
                booking =>
                    booking.ApartmentId == apartmentId &&
                    (booking.Status == BookingStatus.Reserved ||
                     booking.Status == BookingStatus.Confirmed ||
                     booking.Status == BookingStatus.InProgress),
                cancellationToken);
}
