using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Common;

namespace Bookify.Application.Reviews.GetApartmentReviews;

public sealed record GetApartmentReviewsQuery(
    Guid ApartmentId,
    int Page,
    int PageSize) : IQuery<ApartmentReviewsResponse>, IPagedQuery;
