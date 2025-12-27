using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Users.LoginUser;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;

namespace Bookify.Application.Users.LogoutUser;

internal sealed class LogoutUserCommandHandler : ICommandHandler<LogoutUserCommand>
{
    private readonly IJwtService _jwtService;

    public LogoutUserCommandHandler(IJwtService jwtService) 
        => _jwtService = jwtService;

    public async Task<Result> Handle(LogoutUserCommand request, 
        CancellationToken cancellationToken)
    {
        Result result = await _jwtService.RevokeUserAsync(
            request.RefreshToken,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure(result.Error);
        }

        return result;
    }
}
