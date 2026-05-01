using Bookify.Application.Abstractions.Caching;

namespace Bookify.Application.Apartments.GetApartment;

public sealed record GetApartmentQuery(Guid ApartmentId) : ICachedQuery<ApartmentResponse>
{
    public string CacheKey => CacheKeys.Apartment(ApartmentId);
    public TimeSpan? Expiration => TimeSpan.FromMinutes(30);
}
