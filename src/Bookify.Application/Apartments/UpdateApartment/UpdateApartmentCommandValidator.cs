using Bookify.Application.Extensions;
using Bookify.Domain.Shared;
using FluentValidation;

namespace Bookify.Application.Apartments.UpdateApartment;

internal sealed class UpdateApartmentCommandValidator : AbstractValidator<UpdateApartmentCommand>
{
    public UpdateApartmentCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();

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
            .MustBeValidCurrency();

        RuleFor(c => c.CleaningFeeAmount).GreaterThanOrEqualTo(0);

        RuleFor(c => c.CleaningFeeCurrency)
            .NotEmpty()
            .MustBeValidCurrency();

        RuleFor(c => c.Amenities).NotNull();
    }
}
