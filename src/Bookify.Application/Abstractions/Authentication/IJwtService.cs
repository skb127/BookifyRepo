using Bookify.Application.Users;
using Bookify.Domain.Abstractions;

namespace Bookify.Application.Abstractions.Authentication;

public interface IJwtService
{
    Task<Result<AccessTokenResponse>> GetAccessTokenAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<Result<AccessTokenResponse>> GetRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<Result> RevokeUserAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}
