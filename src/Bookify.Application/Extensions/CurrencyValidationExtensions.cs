using Bookify.Domain.Shared;
using FluentValidation;

namespace Bookify.Application.Extensions;

public static class CurrencyValidationExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeValidCurrency<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .Must(currencyCode => Currency.All.Any(c => string.Equals(c.Code, currencyCode, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("The provided currency is invalid or unsupported.");
}
