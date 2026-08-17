using System.Linq.Expressions;
using Bookify.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Infrastructure.Repositories;

internal sealed class InvoiceRepository : Repository<Invoice>, IInvoiceRepository
{
    public InvoiceRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<Invoice?> GetAsync(
        Expression<Func<Invoice, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await DbContext.Set<Invoice>()
            .FirstOrDefaultAsync(predicate, cancellationToken);
}
