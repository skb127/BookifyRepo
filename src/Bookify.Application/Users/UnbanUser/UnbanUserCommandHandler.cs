using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;

namespace Bookify.Application.Users.UnbanUser;

internal sealed class UnbanUserCommandHandler : ICommandHandler<UnbanUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UnbanUserCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UnbanUserCommand request, CancellationToken cancellationToken)
    {
        User? user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        if (user.Status != UserStatus.Suspended)
        {
            return Result.Failure(UserErrors.NotSuspended);
        }

        user.Unban();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
