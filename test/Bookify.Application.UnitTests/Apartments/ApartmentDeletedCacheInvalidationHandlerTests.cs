using Bookify.Application.Abstractions.Caching;
using Bookify.Application.Apartments.DeleteApartment;
using Bookify.Domain.Apartments.Events;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Bookify.Application.UnitTests.Apartments;

public class ApartmentDeletedCacheInvalidationHandlerTests
{
    private static readonly Guid ApartmentId = Guid.CreateVersion7();

    private static readonly ApartmentDeletedDomainEvent DomainEvent = new(ApartmentId);

    private readonly ICacheService _cacheServiceMock;
    private readonly ApartmentDeletedCacheInvalidationDomainEventHandler _handler;

    public ApartmentDeletedCacheInvalidationHandlerTests()
    {
        _cacheServiceMock = Substitute.For<ICacheService>();
        var loggerMock = Substitute.For<ILogger<ApartmentDeletedCacheInvalidationDomainEventHandler>>();

        _handler = new ApartmentDeletedCacheInvalidationDomainEventHandler(_cacheServiceMock, loggerMock);
    }

    [Fact]
    public async Task Handle_ShouldRemoveCacheEntry_WhenApartmentDeleted()
    {
        // Act
        await _handler.Handle(DomainEvent, CancellationToken.None);

        // Assert
        await _cacheServiceMock
            .Received(1)
            .RemoveAsync(
                CacheKeys.Apartment(ApartmentId),
                Arg.Any<CancellationToken>());
    }
}
