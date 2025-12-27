namespace Bookify.Api.Controllers.Bookings;

public sealed record ReserveBookingRequest(
    Guid ApartmentId,
    Guid UserId,
    DateOnly StartDate,
    DateOnly EndDate)
{
    public required Guid ApartmentId { get; init; } = ApartmentId;
    public required Guid UserId { get; init; } = UserId;
    public required DateOnly StartDate { get; init; } = StartDate;
    public required DateOnly EndDate { get; init; } = EndDate;
}
