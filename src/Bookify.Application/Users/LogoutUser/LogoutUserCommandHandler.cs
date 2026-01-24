using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;

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
