using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.TaxRules.GetTaxRule;

public sealed record GetTaxRuleQuery(Guid TaxRuleId) : IQuery<TaxRuleResponse>;
