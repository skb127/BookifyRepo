using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Options;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;
using Microsoft.Extensions.Options;

namespace Bookify.Application.Users.InitiateEmailChange;

internal sealed class InitiateEmailChangeCommandHandler : ICommandHandler<InitiateEmailChangeCommand>
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserContext _userContext;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ExpirationOptions _expirationOptions;

    public InitiateEmailChangeCommandHandler(
        IIdentityProvider identityProvider,
        IUserContext userContext,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IOptions<ExpirationOptions> expirationOptions)
    {
        _identityProvider = identityProvider;
        _userContext = userContext;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _expirationOptions = expirationOptions.Value;
    }

    public async Task<Result> Handle(InitiateEmailChangeCommand request,
        CancellationToken cancellationToken)
    {
        // Get the user from the database
        User? user = await _userRepository.GetOneWithIncludesAsync(
            u => u.Id == _userContext.UserId,
            cancellationToken,
            u => u.EmailChangeToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        // Check if the current password is correct
        bool validCredentials = await _identityProvider.ValidateCredentialsAsync(
            _userContext.Email,
            request.CurrentPassword,
            cancellationToken);

        if (!validCredentials)
        {
            return Result.Failure(UserErrors.InvalidCurrentCredentials);
        }

        // Check if new email already exists in the database
        var newEmail = new Email(request.NewEmail);
        User? userAlreadyExistsDb = await _userRepository.FindOneAsync(u => u.Email == newEmail, cancellationToken);

        if (userAlreadyExistsDb is not null)
        {
            return Result.Failure(UserErrors.EmailAlreadyInUse);
        }

        // Check if new email already exists in the IdP
        bool emailExistsIdp = await _identityProvider.CheckUserByEmailExistsAsync(
            request.NewEmail,
            cancellationToken);

        if (emailExistsIdp)
        {
            return Result.Failure(UserErrors.EmailAlreadyInUse);
        }

        // Initiate email change (creates token and raises domain event)
        user.RequestEmailChange(newEmail, TimeSpan.FromSeconds(_expirationOptions.EmailChangeExpirationSeconds));

        _userRepository.AddEmailChangeToken(user);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
