using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Infrastructure.Repositories;

internal sealed class BookingRepository : Repository<Booking>, IBookingRepository
{
    private static readonly BookingStatus[] ActiveStatuses =
    [
        BookingStatus.Reserved,
        BookingStatus.PendingPayment,
        BookingStatus.Confirmed,
        BookingStatus.InProgress
    ];

    private readonly Bookify.Application.Abstractions.Clock.IDateTimeProvider _dateTimeProvider;

    public BookingRepository(
        ApplicationDbContext dbContext,
        Bookify.Application.Abstractions.Clock.IDateTimeProvider dateTimeProvider)
        : base(dbContext) =>
        _dateTimeProvider = dateTimeProvider;

    public async Task<Booking?> GetWithTaxesAsync(Guid id, CancellationToken cancellationToken = default) =>
        await DbContext
            .Set<Booking>()
            .Include(booking => booking.Taxes)
            .FirstOrDefaultAsync(booking => booking.Id == id, cancellationToken);

    public async Task<Booking?> GetWithRefundAsync(Guid id, CancellationToken cancellationToken = default) =>
        await DbContext
            .Set<Booking>()
            .Include(booking => booking.Refund)
            .FirstOrDefaultAsync(booking => booking.Id == id, cancellationToken);

    public async Task<bool> IsOverlappingAsync(
        Apartment apartment,
        DateRange duration,
        CancellationToken cancellationToken = default)
    {
        DateTime utcNow = _dateTimeProvider.UtcNow;

        return await DbContext
            .Set<Booking>()
            .AnyAsync(
                booking =>
                    booking.ApartmentId == apartment.Id &&
                    booking.Duration.Start <= duration.End &&
                    booking.Duration.End >= duration.Start &&
                    (booking.Status == BookingStatus.Confirmed ||
                     booking.Status == BookingStatus.InProgress ||
                     (booking.Status == BookingStatus.Reserved || booking.Status == BookingStatus.PendingPayment) &&
                     (booking.ExpiresAt == null || booking.ExpiresAt > utcNow)),
                cancellationToken);
    }

    public async Task<bool> HasActiveBookingsAsync(Guid apartmentId, CancellationToken cancellationToken = default) =>
        await DbContext
            .Set<Booking>()
            .AnyAsync(
                booking =>
                    booking.ApartmentId == apartmentId &&
                    ActiveStatuses.Contains(booking.Status),
                cancellationToken);

    public async Task<bool> HasActiveBookingsAsGuestAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await DbContext
            .Set<Booking>()
            .AnyAsync(
                booking =>
                    booking.UserId == userId &&
                    ActiveStatuses.Contains(booking.Status),
                cancellationToken);

    public async Task<bool> HasActiveBookingsAsHostAsync(Guid hostId, CancellationToken cancellationToken = default) =>
        await DbContext
            .Set<Apartment>()
            .Where(apartment => apartment.OwnerId == hostId)
            .AnyAsync(
                apartment => DbContext.Set<Booking>().Any(
                    booking => booking.ApartmentId == apartment.Id && ActiveStatuses.Contains(booking.Status)),
                cancellationToken);
}
