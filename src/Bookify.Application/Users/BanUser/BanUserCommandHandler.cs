using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;

namespace Bookify.Application.Users.BanUser;

internal sealed class BanUserCommandHandler : ICommandHandler<BanUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BanUserCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(BanUserCommand request, CancellationToken cancellationToken)
    {
        User? user = await _userRepository.GetByIdWithRolesAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        if (user.Roles.Any(r => r.Id == Role.Admin.Id))
        {
            return Result.Failure(UserErrors.CannotBanAdmin);
        }

        if (user.Status == UserStatus.Suspended)
        {
            return Result.Failure(UserErrors.AlreadySuspended);
        }

        user.Ban();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
