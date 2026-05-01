using Bookify.Application.Abstractions.Caching;
using Bookify.Application.Users.GetLoggedInUser;

namespace Bookify.Application.Users.GetUserById;

public sealed record GetUserByIdQuery(Guid UserId) : ICachedQuery<UserResponse>
{
    public string CacheKey => CacheKeys.User(UserId);
    public TimeSpan? Expiration => TimeSpan.FromMinutes(30);
}
