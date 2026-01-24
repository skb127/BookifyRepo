using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Common.Interfaces;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;

namespace Bookify.Application.Users.ChangeUserPassword;

internal class ChangeUserPasswordCommandHandler : ICommandHandler<ChangeUserPasswordCommand>
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserContext _userContext;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    
    public ChangeUserPasswordCommandHandler(IIdentityProvider identityProvider,
        IUserContext userContext,
        IUserRepository userRepository, 
        IUnitOfWork unitOfWork)
    {
        _identityProvider = identityProvider;
        _userContext = userContext;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ChangeUserPasswordCommand request, 
        CancellationToken cancellationToken)
    {
        // Get the user from the database
        User? user = await _userRepository.GetByIdAsync(_userContext.UserId, cancellationToken);
    
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }
        
        // Check if the current password is correct in the IdP
        bool validCredentials = await _identityProvider.ValidateCredentialsAsync(_userContext.Email, request.CurrentPassword, cancellationToken);

        if (!validCredentials)
        {
            return Result.Failure(UserErrors.InvalidCurrentCredentials);
        }
        
        Result result = await _identityProvider.ResetPasswordAsync(
            user.IdentityId,
            request.NewPassword,
            cancellationToken);
    
        if (result.IsFailure)
        {
            return Result.Failure(UserErrors.PasswordChangeFailed);
        }
        
        // Update the user entity (PasswordChangedAt + launch the domain event)
        user.ChangePassword();
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        // Update/Reset the password
        return Result.Success();
    }
}
