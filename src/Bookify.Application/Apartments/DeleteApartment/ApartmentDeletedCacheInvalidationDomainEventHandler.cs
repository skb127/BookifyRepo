using Bookify.Application.Abstractions.Caching;
using Bookify.Domain.Apartments.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bookify.Application.Apartments.DeleteApartment;

internal sealed class ApartmentDeletedCacheInvalidationDomainEventHandler
    : INotificationHandler<ApartmentDeletedDomainEvent>
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<ApartmentDeletedCacheInvalidationDomainEventHandler> _logger;

    public ApartmentDeletedCacheInvalidationDomainEventHandler(
        ICacheService cacheService,
        ILogger<ApartmentDeletedCacheInvalidationDomainEventHandler> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task Handle(
        ApartmentDeletedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        string cacheKey = CacheKeys.Apartment(notification.ApartmentId);

        await _cacheService.RemoveAsync(cacheKey, cancellationToken);

        _logger.LogInformation("Cache invalidated for key {CacheKey}", cacheKey);
    }
}
