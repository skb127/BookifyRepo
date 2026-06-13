namespace Bookify.Domain.TaxRules;

public record TaxRate
{
    private TaxRate(decimal value, TaxType type)
    {
        Value = value;
        Type = type;
    }

    public decimal Value { get; init; }
    public TaxType Type { get; init; }

    public static TaxRate Percentage(decimal value) => new(value, TaxType.Percentage);
    public static TaxRate PerNight(decimal value) => new(value, TaxType.FixedPerNight);
    public static TaxRate PerPersonPerNight(decimal value) => new(value, TaxType.FixedPerPersonPerNight);
    public static TaxRate PerBooking(decimal value) => new(value, TaxType.FixedPerBooking);
    public static TaxRate Create(decimal value, TaxType type) => new(value, type);

    public bool IsValid() => Value >= 0;
}
