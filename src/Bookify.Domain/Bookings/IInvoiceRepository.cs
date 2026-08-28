using System.Linq.Expressions;

namespace Bookify.Domain.Bookings;

public interface IInvoiceRepository
{
    Task<Invoice?> GetAsync(Expression<Func<Invoice, bool>> predicate, CancellationToken cancellationToken = default);

    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Invoice invoice);

    void Update(Invoice invoice);
}
