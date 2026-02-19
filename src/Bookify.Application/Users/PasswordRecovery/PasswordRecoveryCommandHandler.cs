using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Options;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;
using Microsoft.Extensions.Options;

namespace Bookify.Application.Users.PasswordRecovery;

internal sealed class PasswordRecoveryCommandHandler : ICommandHandler<PasswordRecoveryCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ExpirationOptions _expirationOptions;

    public PasswordRecoveryCommandHandler(IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IOptions<ExpirationOptions> expirationOptions)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _expirationOptions = expirationOptions.Value;
    }

    public async Task<Result> Handle(PasswordRecoveryCommand request,
        CancellationToken cancellationToken)
    {
        // Get the user from the database with PasswordResetToken
        User? user = await _userRepository.GetOneWithIncludesAsync(
            u => u.Email == new Email(request.Email),
            cancellationToken,
            u => u.PasswordResetToken);

        // If the user doesn't exist, return success to avoid revealing whether the email exists or not 
        if (user is null)
        {
            return Result.Success();
        }

        // If there's already a pending password reset request that hasn't expired, 
        // return success without creating a new one
        if (user.PasswordResetToken is not null && !user.PasswordResetToken.IsExpired)
        {
            return Result.Success();
        }

        user.RequestPasswordRecovery(TimeSpan.FromSeconds(_expirationOptions.PasswordRecoveryExpirationSeconds));

        _userRepository.AddPasswordResetToken(user);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
