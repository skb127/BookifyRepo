using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Shared;
using Bookify.Domain.Users;

namespace Bookify.Application.Users.RegisterHost;

internal sealed class RegisterHostCommandHandler : ICommandHandler<RegisterHostCommand, Guid>
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdentityProvider _identityProvider;

    public RegisterHostCommandHandler(
        IAuthenticationService authenticationService,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IIdentityProvider identityProvider)
    {
        _authenticationService = authenticationService;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _identityProvider = identityProvider;
    }

    public async Task<Result<Guid>> Handle(
        RegisterHostCommand request,
        CancellationToken cancellationToken)
    {
        User? userAlreadyExistsDb =
            await _userRepository.FindOneAsync(user => user.Email == new Email(request.Email), cancellationToken);

        if (userAlreadyExistsDb is not null)
        {
            return Result.Failure<Guid>(UserErrors.AlreadyExists);
        }

        bool userAlreadyExistsInProvider = await _identityProvider
            .CheckUserByEmailExistsAsync(request.Email, cancellationToken);

        if (userAlreadyExistsInProvider)
        {
            return Result.Failure<Guid>(UserErrors.AlreadyExists);
        }

        var user = User.Create(
            new FirstName(request.FirstName),
            new LastName(request.LastName),
            new Email(request.Email),
            DateOfBirth.Create(request.DateOfBirth!.Value),
            Role.Host);

        user.SetPhoneNumber(PhoneNumber.Create(request.PhoneNumber)!);

        string identityId = await _authenticationService.RegisterAsync(
            user,
            request.Password,
            cancellationToken);

        user.SetIdentityId(identityId);

        _userRepository.Add(user);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
