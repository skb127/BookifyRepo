using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Abstractions.Caching;

// Generic cached query interface, returns a response of type TResponse, and it implements IQuery<TResponse> and ICachedQuery
public interface ICachedQuery<TResponse> : IQuery<TResponse>, ICachedQuery;

public interface ICachedQuery
{
    string CacheKey { get; }

    TimeSpan? Expiration { get; }
}
