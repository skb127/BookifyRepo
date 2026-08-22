namespace Bookify.Api.Controllers.Bookings.Requests;

public sealed record ReserveBookingRequest(
    Guid ApartmentId,
    DateOnly StartDate,
    DateOnly EndDate,
    int GuestCount = 1)
{
    public required Guid ApartmentId { get; init; } = ApartmentId;
    public required DateOnly StartDate { get; init; } = StartDate;
    public required DateOnly EndDate { get; init; } = EndDate;
}
