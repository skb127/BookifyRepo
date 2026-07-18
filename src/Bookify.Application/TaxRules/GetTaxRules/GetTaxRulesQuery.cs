using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.TaxRules.GetTaxRules;

public sealed record GetTaxRulesQuery(string? CountryCode = null) : IQuery<IReadOnlyList<TaxRuleResponse>>;
