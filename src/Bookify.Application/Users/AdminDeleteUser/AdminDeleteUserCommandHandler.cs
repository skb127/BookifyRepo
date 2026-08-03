using Bookify.Application.Abstractions.Caching;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;

namespace Bookify.Application.Users.AdminDeleteUser;

internal sealed class AdminDeleteUserCommandHandler : ICommandHandler<AdminDeleteUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;

    public AdminDeleteUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
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

        await _cacheService.RemoveAsync(CacheKeys.User(request.UserId), cancellationToken);

        return Result.Success();
    }
}
