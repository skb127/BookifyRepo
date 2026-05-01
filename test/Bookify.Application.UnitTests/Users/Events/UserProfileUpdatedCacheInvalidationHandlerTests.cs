using Bookify.Application.Abstractions.Caching;
using Bookify.Application.Users.UpdateUserProfile;
using Bookify.Domain.Users.Events;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Bookify.Application.UnitTests.Users.Events;

public class UserProfileUpdatedCacheInvalidationHandlerTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();

    private static readonly UserProfileUpdatedDomainEvent DomainEvent = new(UserId);

    private readonly ICacheService _cacheServiceMock;
    private readonly UserProfileUpdatedCacheInvalidationDomainEventHandler _handler;

    public UserProfileUpdatedCacheInvalidationHandlerTests()
    {
        _cacheServiceMock = Substitute.For<ICacheService>();
        var loggerMock = Substitute.For<ILogger<UserProfileUpdatedCacheInvalidationDomainEventHandler>>();

        _handler = new UserProfileUpdatedCacheInvalidationDomainEventHandler(_cacheServiceMock, loggerMock);
    }

    [Fact]
    public async Task Handle_ShouldRemoveCacheEntry_WhenUserProfileUpdated()
    {
        // Act
        await _handler.Handle(DomainEvent, CancellationToken.None);

        // Assert
        await _cacheServiceMock
            .Received(1)
            .RemoveAsync(
                CacheKeys.User(UserId),
                Arg.Any<CancellationToken>());
    }
}
