namespace Bookify.Application.TaxRules;

public sealed class TaxRuleResponse
{
    public Guid Id { get; init; }
    public string CountryCode { get; init; } = "";
    public string? Region { get; init; }
    public string? City { get; init; }
    public decimal RateValue { get; init; }
    public int RateType { get; init; }
    public string Name { get; init; } = "";
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public bool IsActive { get; init; }
}
