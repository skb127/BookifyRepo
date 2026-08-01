using Bookify.Application.Abstractions.Caching;

namespace Bookify.Application.Users.GetUserById;

public sealed record GetUserByIdQuery(Guid UserId) : ICachedQuery<AdminUserResponse>
{
    public string CacheKey => CacheKeys.User(UserId);
    public TimeSpan? Expiration => TimeSpan.FromMinutes(30);
}
