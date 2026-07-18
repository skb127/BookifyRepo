using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.TaxRules.UpdateTaxRule;

public sealed record UpdateTaxRuleCommand(
    Guid Id,
    string CountryCode,
    string? Region,
    string? City,
    decimal RateValue,
    int RateType,
    string Name,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo) : ICommand;
