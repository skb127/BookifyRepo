using Bookify.Application.Extensions;
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
            .MustBeValidCurrency();

        RuleFor(c => c.CleaningFeeAmount).GreaterThanOrEqualTo(0);

        RuleFor(c => c.CleaningFeeCurrency)
            .NotEmpty()
            .MustBeValidCurrency();

        RuleFor(c => c.Amenities).NotNull();

        RuleFor(c => c.MinimumNights).GreaterThan(0);
        RuleFor(c => c.CheckInCutOffHours).InclusiveBetween(0, 48);

        RuleFor(c => c.BaseGuests).GreaterThanOrEqualTo(1);
        RuleFor(c => c.MaxGuests).GreaterThanOrEqualTo(c => c.BaseGuests);
        RuleFor(c => c.ExtraGuestFeeAmount).GreaterThanOrEqualTo(0);
        RuleFor(c => c.ExtraGuestFeeCurrency)
            .NotEmpty()
            .MustBeValidCurrency()
            .Equal(c => c.PriceCurrency)
            .WithMessage("Extra guest fee currency must match the price currency.")
            .When(c => c.ExtraGuestFeeAmount > 0);
    }
}
