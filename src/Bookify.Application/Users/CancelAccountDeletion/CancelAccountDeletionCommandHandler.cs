using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Abstractions.Scheduling;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Shared;
using Bookify.Domain.Users;

namespace Bookify.Application.Users.CancelAccountDeletion;

internal sealed class CancelAccountDeletionCommandHandler : ICommandHandler<CancelAccountDeletionCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJobScheduler _jobScheduler;

    public CancelAccountDeletionCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IJobScheduler jobScheduler)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _jobScheduler = jobScheduler;
    }

    public async Task<Result> Handle(CancelAccountDeletionCommand request, CancellationToken cancellationToken)
    {
        string tokenHash = SecurityUtils.ComputeSha256Hash(request.Token);

        User? user = await _userRepository.FindByAccountDeletionTokenAsync(tokenHash, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.InvalidToken);
        }

        if (user.AccountDeletionToken is null || user.AccountDeletionToken.IsExpired)
        {
            return Result.Failure(UserErrors.DeletionWindowExpired);
        }

        user.CancelDeletion();

        await _jobScheduler.CancelAccountDeletionAsync(user.Id, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
