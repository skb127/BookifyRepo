using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;

namespace Bookify.Application.Users.AdminDeleteUser;

internal sealed class AdminDeleteUserCommandHandler : ICommandHandler<AdminDeleteUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AdminDeleteUserCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(AdminDeleteUserCommand request, CancellationToken cancellationToken)
    {
        User? user = await _userRepository.GetByIdWithRolesAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        if (user.Roles.Any(r => r.Id == Role.Admin.Id))
        {
            int adminCount = await _userRepository.CountAdminsAsync(cancellationToken);
            if (adminCount <= 1)
            {
                return Result.Failure(UserErrors.CannotDeleteLastAdmin);
            }
        }

        user.Delete();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
