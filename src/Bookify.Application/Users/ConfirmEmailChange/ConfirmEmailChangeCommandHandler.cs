using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Shared;
using Bookify.Domain.Users;

namespace Bookify.Application.Users.ConfirmEmailChange;

internal sealed class ConfirmEmailChangeCommandHandler : ICommandHandler<ConfirmEmailChangeCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmEmailChangeCommandHandler(
        IUserRepository userRepository,
        IIdentityProvider identityProvider,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _identityProvider = identityProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ConfirmEmailChangeCommand request, CancellationToken cancellationToken)
    {
        string tokenHash = SecurityUtils.ComputeSha256Hash(request.Token);

        User? user = await _userRepository.GetOneWithIncludesAsync(
            u => u.EmailChangeToken != null && u.EmailChangeToken.TokenHash == tokenHash,
            cancellationToken,
            u => u.EmailChangeToken);

        if (user?.EmailChangeToken is null || user.EmailChangeToken.IsExpired)
        {
            return Result.Failure(UserErrors.InvalidEmailChangeToken);
        }

        Result identityResult = await _identityProvider.UpdateUserEmailAsync(
            user.IdentityId,

            user.EmailChangeToken.PendingEmail.Value,

            cancellationToken);

        if (identityResult.IsFailure)
        {
            return Result.Failure(UserErrors.InvalidEmailChangeToken);
        }

        user.ConfirmEmailChange();

        await _identityProvider.LogoutAllSessionsAsync(user.IdentityId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
