using Bookify.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Infrastructure.Repositories;

internal sealed class InvoiceRepository : Repository<Invoice>, IInvoiceRepository
{
    public InvoiceRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<Invoice?> GetByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        await DbContext.Set<Invoice>()
            .FirstOrDefaultAsync(i => i.BookingId == bookingId, cancellationToken);
}
