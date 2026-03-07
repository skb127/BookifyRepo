using Bookify.Application.Common;

namespace Bookify.Application.Reviews.GetApartmentReviews;

public sealed class ApartmentReviewsResponse : PagedResponse<ReviewSummaryResponse>
{
    public double AverageRating { get; init; }
}
