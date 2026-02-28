namespace Bookify.Application.Apartments.CheckApartmentAvailability;

public sealed record ApartmentAvailabilityResponse(
    bool IsAvailable,
    DateOnly StartDate,
    DateOnly EndDate);
