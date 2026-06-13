using Bookify.Domain.Abstractions;

namespace Bookify.Domain.TaxRules;

public static class TaxRuleErrors
{
    public static Error NotFound { get; } = new(
        "TaxRule.NotFound",
        "The tax rule with the specified identifier was not found");
}
