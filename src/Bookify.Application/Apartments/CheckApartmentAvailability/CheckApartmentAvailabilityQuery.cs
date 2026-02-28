using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Apartments.CheckApartmentAvailability;

public sealed record CheckApartmentAvailabilityQuery(
    Guid ApartmentId,
    DateOnly StartDate,
    DateOnly EndDate) : IQuery<ApartmentAvailabilityResponse>;
