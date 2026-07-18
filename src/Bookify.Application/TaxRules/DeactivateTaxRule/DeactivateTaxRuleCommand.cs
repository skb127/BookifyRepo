using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.TaxRules.DeactivateTaxRule;

public sealed record DeactivateTaxRuleCommand(Guid Id) : ICommand;
