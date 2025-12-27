namespace Bookify.Api.Controllers.Reviews;

public sealed record AddReviewRequest(Guid BookingId, int Rating, string Comment)
{
    public required Guid BookingId { get; init; } = BookingId;
    public required int Rating { get; init; } = Rating;
    public required string Comment { get; init; } = Comment;
}
