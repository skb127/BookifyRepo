using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;

namespace Bookify.Application.Users.RevokeAllSessions;

internal sealed class RevokeAllSessionsCommandHandler : ICommandHandler<RevokeAllSessionsCommand>
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserContext _userContext;

    public RevokeAllSessionsCommandHandler(
        IIdentityProvider identityProvider,
        IUserContext userContext)
    {
        _identityProvider = identityProvider;
        _userContext = userContext;
    }

    public async Task<Result> Handle(
        RevokeAllSessionsCommand request,
        CancellationToken cancellationToken)
    {
        Result result = await _identityProvider.LogoutAllSessionsAsync(
            _userContext.IdentityId,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure(result.Error);
        }

        return Result.Success();
    }
}
