using Bookify.Application.Abstractions.Caching;
using Bookify.Domain.Users.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bookify.Application.Users.UpdateUserProfile;

internal sealed class UserProfileUpdatedCacheInvalidationDomainEventHandler
    : INotificationHandler<UserProfileUpdatedDomainEvent>
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<UserProfileUpdatedCacheInvalidationDomainEventHandler> _logger;

    public UserProfileUpdatedCacheInvalidationDomainEventHandler(
        ICacheService cacheService,
        ILogger<UserProfileUpdatedCacheInvalidationDomainEventHandler> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task Handle(
        UserProfileUpdatedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        string cacheKey = CacheKeys.User(notification.UserId);

        await _cacheService.RemoveAsync(cacheKey, cancellationToken);

        _logger.LogInformation("Cache invalidated for key {CacheKey}", cacheKey);
    }
}
