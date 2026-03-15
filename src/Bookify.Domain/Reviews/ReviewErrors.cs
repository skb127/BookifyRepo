using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Reviews;

public static class ReviewErrors
{
    public static readonly Error NotEligible = new(
        "Review.NotEligible",
        "The review is not eligible because the booking is not yet completed");

    public static readonly Error Unauthorized = new(
        "Review.Unauthorized",
        "You are not authorized to review this booking");

    public static readonly Error AlreadyReviewed = new(
        "Review.AlreadyReviewed",
        "A review for this booking already exists");

    public static readonly Error NotFound = new(
        "Review.NotFound",
        "The review with the specified identifier was not found");

    public static readonly Error NotAuthor = new(
        "Review.NotAuthor",
        "You are not the author of this review");

    public static readonly Error EditTimeExpired = new(
        "Review.EditTimeExpired",
        "The allowed time to edit this review has expired. Reviews can only be modified within 7 days after the booking is completed.");
}
