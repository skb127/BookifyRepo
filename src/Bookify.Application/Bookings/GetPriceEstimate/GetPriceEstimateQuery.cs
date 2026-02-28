using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Bookings.GetPriceEstimate;

public sealed record GetPriceEstimateQuery(
    Guid ApartmentId,
    DateOnly StartDate,
    DateOnly EndDate) : IQuery<PriceEstimateResponse>;
