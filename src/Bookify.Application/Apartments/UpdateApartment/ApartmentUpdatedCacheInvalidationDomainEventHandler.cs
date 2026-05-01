using Bookify.Application.Abstractions.Caching;
using Bookify.Domain.Apartments.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bookify.Application.Apartments.UpdateApartment;

internal sealed class ApartmentUpdatedCacheInvalidationDomainEventHandler
    : INotificationHandler<ApartmentUpdatedDomainEvent>
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<ApartmentUpdatedCacheInvalidationDomainEventHandler> _logger;

    public ApartmentUpdatedCacheInvalidationDomainEventHandler(
        ICacheService cacheService,
        ILogger<ApartmentUpdatedCacheInvalidationDomainEventHandler> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task Handle(
        ApartmentUpdatedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        string cacheKey = CacheKeys.Apartment(notification.ApartmentId);

        await _cacheService.RemoveAsync(cacheKey, cancellationToken);

        _logger.LogInformation("Cache invalidated for key {CacheKey}", cacheKey);
    }
}
