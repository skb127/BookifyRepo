using Bookify.Application.Common;
using Bookify.Application.Extensions;
using FluentValidation;

namespace Bookify.Application.Apartments.SearchApartments;

internal sealed class SearchApartmentsQueryValidator : PagedQueryValidator<SearchApartmentsQuery>
{
    public SearchApartmentsQueryValidator()
    {
        // Date range: both must be provided together and start <= end
        RuleFor(q => q.EndDate)
            .NotNull()
            .WithMessage("'EndDate' is required when 'StartDate' is provided.")
            .When(q => q.StartDate.HasValue);

        RuleFor(q => q.StartDate)
            .NotNull()
            .WithMessage("'StartDate' is required when 'EndDate' is provided.")
            .When(q => q.EndDate.HasValue);

        RuleFor(q => q.StartDate)
            .LessThanOrEqualTo(q => q.EndDate!.Value)
            .WithMessage("'StartDate' must be less than or equal to 'EndDate'.")
            .When(q => q.StartDate.HasValue && q.EndDate.HasValue);

        // Price range
        RuleFor(q => q.MinPrice)
            .GreaterThanOrEqualTo(0)
            .When(q => q.MinPrice.HasValue);

        RuleFor(q => q.MaxPrice)
            .GreaterThanOrEqualTo(0)
            .When(q => q.MaxPrice.HasValue);

        RuleFor(q => q.MinPrice)
            .LessThanOrEqualTo(q => q.MaxPrice!.Value)
            .WithMessage("'MinPrice' must be less than or equal to 'MaxPrice'.")
            .When(q => q.MinPrice.HasValue && q.MaxPrice.HasValue);

        RuleFor(q => q.Currency!)
            .MustBeValidCurrency()
            .When(q => !string.IsNullOrWhiteSpace(q.Currency));
    }
}
