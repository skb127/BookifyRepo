using Bookify.Domain.Abstractions;

namespace Bookify.Domain.TaxRules;

public static class TaxRuleErrors
{
    public static readonly Error NotFound = new(
        "TaxRule.NotFound",
        "The tax rule with the specified identifier was not found");

    public static readonly Error AlreadyInactive = new(
        "TaxRule.AlreadyInactive",
        "The tax rule is already inactive");
}
