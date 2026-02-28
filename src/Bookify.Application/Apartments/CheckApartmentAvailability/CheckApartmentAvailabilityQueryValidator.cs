using FluentValidation;

namespace Bookify.Application.Apartments.CheckApartmentAvailability;

public sealed class CheckApartmentAvailabilityQueryValidator : AbstractValidator<CheckApartmentAvailabilityQuery>
{
    public CheckApartmentAvailabilityQueryValidator() =>
        RuleFor(x => x.StartDate)
            .LessThanOrEqualTo(x => x.EndDate)
            .WithMessage("End date must be greater than or equal to start date.");
}
