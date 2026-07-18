namespace Bookify.Api.Controllers.TaxRules;

public sealed record CreateTaxRuleRequest(
    string CountryCode,
    string? Region,
    string? City,
    decimal RateValue,
    int RateType,
    string Name,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo)
{
    public required decimal RateValue { get; init; } = RateValue;
    public required int RateType { get; init; } = RateType;
    public required DateOnly EffectiveFrom { get; init; } = EffectiveFrom;
}
