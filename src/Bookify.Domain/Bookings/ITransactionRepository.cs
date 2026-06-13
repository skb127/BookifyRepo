namespace Bookify.Domain.Bookings;

public interface ITransactionRepository
{
    Task<Transaction?> GetByStripeSessionIdAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<Transaction?> GetByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default);

    void Add(Transaction transaction);
}
