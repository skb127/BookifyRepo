using Bookify.Domain.Abstractions;
using Bookify.Domain.Shared;

namespace Bookify.Domain.TaxRules;

public sealed class TaxRule : Entity
{
    private TaxRule(
        Guid id,
        string countryCode,
        string? region,
        string? city,
        TaxRate rate,
        string name,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        bool isActive,
        DateTime createdOnUtc)
        : base(id)
    {
        CountryCode = countryCode;
        Region = region;
        City = city;
        Rate = rate;
        Name = name;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        IsActive = isActive;
        CreatedOnUtc = createdOnUtc;
    }

    private TaxRule()
    {
    }

    public string CountryCode { get; private set; } = null!;
    public string? Region { get; private set; }
    public string? City { get; private set; }
    public TaxRate Rate { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? UpdatedOnUtc { get; private set; }

    public static TaxRule Create(
        string countryCode,
        string? region,
        string? city,
        TaxRate rate,
        string name,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        DateTime utcNow) =>
        new(
            Guid.CreateVersion7(),
            countryCode,
            region,
            city,
            rate,
            name,
            effectiveFrom,
            effectiveTo,
            isActive: true,
            createdOnUtc: utcNow);

    public Money CalculateTaxAmount(Money totalPrice, int nights, int guests)
    {
        decimal amount = Rate.Type switch
        {
            TaxType.Percentage => totalPrice.Amount * Rate.Value,
            TaxType.FixedPerNight => Rate.Value * nights,
            TaxType.FixedPerPersonPerNight => Rate.Value * nights * guests,
            TaxType.FixedPerBooking => Rate.Value,
            _ => throw new InvalidOperationException("Unknown tax type")
        };

        return totalPrice with { Amount = amount };
    }

    public void Deactivate(DateTime utcNow)
    {
        IsActive = false;
        UpdatedOnUtc = utcNow;
    }

    public void Update(
        string countryCode,
        string? region,
        string? city,
        TaxRate rate,
        string name,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        DateTime utcNow)
    {
        CountryCode = countryCode;
        Region = region;
        City = city;
        Rate = rate;
        Name = name;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        UpdatedOnUtc = utcNow;
    }
}
