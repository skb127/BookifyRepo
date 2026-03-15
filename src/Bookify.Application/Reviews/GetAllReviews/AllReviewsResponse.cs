namespace Bookify.Application.Reviews.GetAllReviews;

public sealed record AllReviewsResponse(
    Guid Id,
    Guid BookingId,
    Guid ApartmentId,
    Guid UserId,
    int Rating,
    string Comment,
    DateTime CreatedOnUtc,
    DateTime? EditedOnUtc);
