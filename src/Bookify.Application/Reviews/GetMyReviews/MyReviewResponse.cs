namespace Bookify.Application.Reviews.GetMyReviews;

public sealed class MyReviewResponse
{
    public Guid Id { get; init; }

    public Guid BookingId { get; init; }

    public Guid ApartmentId { get; init; }

    public int Rating { get; init; }

    public string Comment { get; init; } = "";

    public DateTime CreatedOnUtc { get; init; }
}
