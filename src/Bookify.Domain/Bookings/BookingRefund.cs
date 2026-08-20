namespace Bookify.Domain.Bookings;

public sealed class BookingRefund
{
    private BookingRefund() { } // EF Core

    private BookingRefund(Guid id, decimal amount, string currency, string reason, DateTime initiatedOnUtc)
    {
        Id = id;
        Amount = amount;
        Currency = currency;
        Reason = reason;
        InitiatedOnUtc = initiatedOnUtc;
    }

    public Guid Id { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public DateTime InitiatedOnUtc { get; private set; }

    internal static BookingRefund Create(decimal amount, string currency, string reason, DateTime utcNow) =>
        new(Guid.CreateVersion7(), amount, currency, reason, utcNow);
}
