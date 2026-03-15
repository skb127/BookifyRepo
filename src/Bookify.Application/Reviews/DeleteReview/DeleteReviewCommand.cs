using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Reviews.DeleteReview;

public record DeleteReviewCommand(Guid ReviewId) : ICommand;
