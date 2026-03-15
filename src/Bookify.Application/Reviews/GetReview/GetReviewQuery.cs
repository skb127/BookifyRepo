using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Reviews.GetReview;

public sealed record GetReviewQuery(Guid ReviewId) : IQuery<ReviewResponse>;
