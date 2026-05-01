using Bookify.Application.Abstractions.Caching;
using Bookify.Application.Apartments.UpdateApartment;
using Bookify.Domain.Apartments.Events;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Bookify.Application.UnitTests.Apartments;

public class ApartmentUpdatedCacheInvalidationHandlerTests
{
    private static readonly Guid ApartmentId = Guid.CreateVersion7();

    private static readonly ApartmentUpdatedDomainEvent DomainEvent = new(ApartmentId);

    private readonly ICacheService _cacheServiceMock;
    private readonly ApartmentUpdatedCacheInvalidationDomainEventHandler _handler;

    public ApartmentUpdatedCacheInvalidationHandlerTests()
    {
        _cacheServiceMock = Substitute.For<ICacheService>();
        var loggerMock = Substitute.For<ILogger<ApartmentUpdatedCacheInvalidationDomainEventHandler>>();

        _handler = new ApartmentUpdatedCacheInvalidationDomainEventHandler(_cacheServiceMock, loggerMock);
    }

    [Fact]
    public async Task Handle_ShouldRemoveCacheEntry_WhenApartmentUpdated()
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
