using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Common;

namespace Bookify.Application.Reviews.GetMyReviews;

public sealed record GetMyReviewsQuery(
    int Page,
    int PageSize) : IQuery<PagedResponse<MyReviewResponse>>, IPagedQuery;
