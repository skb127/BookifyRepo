using Bookify.Domain.Abstractions;
using Bookify.Domain.Shared;
using Bookify.Domain.TaxRules;

namespace Bookify.Domain.Bookings;

public sealed class BookingTax : Entity
{
    private BookingTax(
        Guid id,
        Guid bookingId,
        string taxRuleName,
        TaxType taxType,
        decimal rate,
        Money calculatedAmount,
        DateTime createdOnUtc)
        : base(id)
    {
        BookingId = bookingId;
        TaxRuleName = taxRuleName;
        TaxType = taxType;
        Rate = rate;
        CalculatedAmount = calculatedAmount;
        CreatedOnUtc = createdOnUtc;
    }

    private BookingTax()
    {
    }

    public Guid BookingId { get; private set; }
    public string TaxRuleName { get; private set; } = null!;
    public TaxType TaxType { get; private set; }
    public decimal Rate { get; private set; }
    public Money CalculatedAmount { get; private set; } = null!;
    public DateTime CreatedOnUtc { get; private set; }

    public static BookingTax CreateSnapshot(
        Guid id,
        Guid bookingId,
        TaxRule rule,
        Money calculatedAmount,
        DateTime utcNow) =>
        new(
            id,
            bookingId,
            rule.Name,
            rule.Rate.Type,
            rule.Rate.Value,
            calculatedAmount,
            utcNow);
}
