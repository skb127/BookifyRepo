using Bookify.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Infrastructure.Repositories;

internal sealed class TransactionRepository : Repository<Transaction>, ITransactionRepository
{
    public TransactionRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<Transaction?> GetByStripeSessionIdAsync(string sessionId, CancellationToken cancellationToken = default) =>
        await DbContext.Set<Transaction>()
            .FirstOrDefaultAsync(t => t.StripeSessionId == sessionId, cancellationToken);

    public async Task<Transaction?> GetByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        await DbContext.Set<Transaction>()
            .FirstOrDefaultAsync(t => t.BookingId == bookingId, cancellationToken);
}
