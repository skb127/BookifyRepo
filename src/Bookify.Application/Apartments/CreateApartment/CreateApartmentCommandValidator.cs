using Bookify.Domain.Shared;
using FluentValidation;

namespace Bookify.Application.Apartments.CreateApartment;

internal sealed class CreateApartmentCommandValidator : AbstractValidator<CreateApartmentCommand>
{
    public CreateApartmentCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);

        RuleFor(c => c.Description).NotEmpty().MaximumLength(2000);

        RuleFor(c => c.Country).NotEmpty();
        RuleFor(c => c.State).NotEmpty();
        RuleFor(c => c.ZipCode).NotEmpty();
        RuleFor(c => c.City).NotEmpty();
        RuleFor(c => c.Street).NotEmpty();

        RuleFor(c => c.PriceAmount).GreaterThan(0);

        RuleFor(c => c.PriceCurrency)
            .NotEmpty()
            .Must(BeValidCurrency)
            .WithMessage("The provided currency is invalid or unsupported.");

        RuleFor(c => c.CleaningFeeAmount).GreaterThanOrEqualTo(0);

        RuleFor(c => c.CleaningFeeCurrency)
            .NotEmpty()
            .Must(BeValidCurrency)
            .WithMessage("The provided currency is invalid or unsupported.");

        RuleFor(c => c.Amenities).NotNull();
    }

    private bool BeValidCurrency(string currencyCode) =>
        Currency.All.Any(c => string.Equals(c.Code, currencyCode, StringComparison.OrdinalIgnoreCase));
}
