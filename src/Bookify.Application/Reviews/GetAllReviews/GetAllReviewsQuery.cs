using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Common;

namespace Bookify.Application.Reviews.GetAllReviews;

public sealed record GetAllReviewsQuery(
    Guid? ApartmentId,
    Guid? UserId,
    int? MinRating,
    int? MaxRating,
    bool? IsEdited,
    int Page,
    int PageSize) : IQuery<PagedResponse<AllReviewsResponse>>, IPagedQuery;
