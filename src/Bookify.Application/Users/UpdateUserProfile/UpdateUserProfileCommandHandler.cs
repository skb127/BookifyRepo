using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Shared;
using Bookify.Domain.Users;

namespace Bookify.Application.Users.UpdateUserProfile;

internal sealed class UpdateUserProfileCommandHandler : ICommandHandler<UpdateUserProfileCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserContext _userContext;
    private readonly IIdentityProvider _identityProvider;

    public UpdateUserProfileCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        IIdentityProvider identityProvider)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
        _identityProvider = identityProvider;
    }

    public async Task<Result> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        User? user = await _userRepository.GetByIdAsync(_userContext.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        // Verify Password
        bool isPasswordValid = await _identityProvider.ValidateCredentialsAsync(
            user.Email.Value,
            request.Password,
            cancellationToken);

        if (!isPasswordValid)
        {
            return Result.Failure(UserErrors.InvalidCredentials);
        }

        // Update Identity Provider (Keycloak) first
        Result identityResult = await _identityProvider.UpdateUserProfileAsync(
            user.IdentityId,
            request.FirstName,
            request.LastName,
            cancellationToken);

        if (identityResult.IsFailure)
        {
            // If IdP update fails, we stop here and don't update the local DB
            return Result.Failure(UserErrors.UpdateFailed);
        }

        // Update Local Database
        PhoneNumber? phoneNumber = null;
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            phoneNumber = PhoneNumber.Create(request.PhoneNumber);
        }

        var dateOfBirth = DateOfBirth.Create(request.DateOfBirth);

        user.UpdateProfile(
            new FirstName(request.FirstName),
            new LastName(request.LastName),
            phoneNumber,
            dateOfBirth);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
