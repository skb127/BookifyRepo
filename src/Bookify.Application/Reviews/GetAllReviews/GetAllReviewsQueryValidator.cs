using Bookify.Application.Common;
using FluentValidation;

namespace Bookify.Application.Reviews.GetAllReviews;

internal sealed class GetAllReviewsQueryValidator : PagedQueryValidator<GetAllReviewsQuery>
{
    public GetAllReviewsQueryValidator()
    {
        RuleFor(q => q.MinRating)
            .InclusiveBetween(1, 5)
            .When(q => q.MinRating.HasValue);
            
        RuleFor(q => q.MaxRating)
            .InclusiveBetween(1, 5)
            .When(q => q.MaxRating.HasValue);
    }
}
