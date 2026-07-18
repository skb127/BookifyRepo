using FluentValidation;

namespace Bookify.Application.TaxRules.UpdateTaxRule;

internal sealed class UpdateTaxRuleCommandValidator : AbstractValidator<UpdateTaxRuleCommand>
{
    public UpdateTaxRuleCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();

        RuleFor(c => c.CountryCode)
            .NotEmpty()
            .Length(2);

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(c => c.RateValue)
            .GreaterThanOrEqualTo(0);

        RuleFor(c => c.RateType)
            .Must(type => type >= 1 && type <= 4)
            .WithMessage("Rate type must be a valid TaxType.");

        RuleFor(c => c.EffectiveFrom)
            .NotEmpty();

        RuleFor(c => c.EffectiveTo)
            .GreaterThan(c => c.EffectiveFrom)
            .When(c => c.EffectiveTo.HasValue);
    }
}
