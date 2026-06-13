namespace Bookify.Domain.Bookings;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Invoice invoice);

    void Update(Invoice invoice);
}
