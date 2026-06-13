namespace Bookify.Infrastructure.HostBalances;

public sealed class HostBalance
{
    private HostBalance(Guid id, Guid hostId, Guid bookingId, decimal amount, string currency, string reason, DateTime createdOnUtc)
    {
        Id = id;
        HostId = hostId;
        BookingId = bookingId;
        Amount = amount;
        Currency = currency;
        Reason = reason;
        CreatedOnUtc = createdOnUtc;
    }

    private HostBalance()
    {
    }

    public Guid Id { get; private set; }
    public Guid HostId { get; private set; }
    public Guid BookingId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public DateTime CreatedOnUtc { get; private set; }

    public static HostBalance Create(Guid hostId, Guid bookingId, decimal amount, string currency, string reason, DateTime utcNow) =>
        new HostBalance(
            Guid.CreateVersion7(),
            hostId,
            bookingId,
            amount,
            currency,
            reason,
            utcNow);
}
