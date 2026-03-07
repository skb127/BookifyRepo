using FluentValidation;

namespace Bookify.Application.Bookings.GetPriceEstimate;

internal sealed class GetPriceEstimateQueryValidator : AbstractValidator<GetPriceEstimateQuery>
{
    public GetPriceEstimateQueryValidator()
    {
        RuleFor(c => c.ApartmentId).NotEmpty();

        RuleFor(c => c.StartDate).NotEmpty();

        RuleFor(c => c.EndDate)
            .NotEmpty()
            .GreaterThan(c => c.StartDate);
    }
}
