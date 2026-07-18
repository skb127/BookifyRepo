using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.TaxRules.CreateTaxRule;

public sealed record CreateTaxRuleCommand(
    string CountryCode,
    string? Region,
    string? City,
    decimal RateValue,
    int RateType,
    string Name,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo) : ICommand<Guid>;
