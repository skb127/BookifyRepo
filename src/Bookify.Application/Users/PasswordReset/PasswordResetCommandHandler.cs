using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Shared;
using Bookify.Domain.Users;

namespace Bookify.Application.Users.PasswordReset;

internal sealed class PasswordResetCommandHandler : ICommandHandler<PasswordResetCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdentityProvider _identityProvider;

    public PasswordResetCommandHandler(IUserRepository userRepository,
        IUnitOfWork unitOfWork, IIdentityProvider identityProvider)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _identityProvider = identityProvider;
    }

    public async Task<Result> Handle(PasswordResetCommand request,
        CancellationToken cancellationToken)
    {
        // Token hash
        string tokenHash = SecurityUtils.ComputeSha256Hash(request.Token);

        // Get the user info from the database using the token hash
        User? user = await _userRepository.GetOneWithIncludesAsync(
            u => u.PasswordResetToken != null && u.PasswordResetToken.TokenHash == tokenHash,
            cancellationToken,
            u => u.PasswordResetToken);

        // If the user with the provided token doesn't exist or the token is expired, return an error
        if (user is null || user.PasswordResetToken is { IsExpired: true })
        {
            return Result.Failure(UserErrors.InvalidToken);
        }

        // Reset the user password in the identity provider
        Result identityResult = await _identityProvider.ResetPasswordAsync(
            user.IdentityId,
            request.NewPassword,
            cancellationToken);

        if (identityResult.IsFailure)
        {
            return Result.Failure(UserErrors.PasswordResetFailed);
        }

        user.ResetPassword();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
