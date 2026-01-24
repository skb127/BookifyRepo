using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;

namespace Bookify.Application.Users.RefreshTokenUser;

internal class RefreshTokenUserCommandHandler : ICommandHandler<RefreshTokenUserCommand, AccessTokenResponse>
{
    private readonly IJwtService _jwtService;

    public RefreshTokenUserCommandHandler(IJwtService jwtService)
        => _jwtService = jwtService;

    public async Task<Result<AccessTokenResponse>> Handle(RefreshTokenUserCommand request,
        CancellationToken cancellationToken)
    {
        Result<AccessTokenResponse> result = await _jwtService.GetRefreshTokenAsync(
            request.RefreshToken,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<AccessTokenResponse>(UserErrors.InvalidCredentials);
        }

        return result;
    }
}
