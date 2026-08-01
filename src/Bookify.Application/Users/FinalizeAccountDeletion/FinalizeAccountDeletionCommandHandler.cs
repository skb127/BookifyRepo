using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;

namespace Bookify.Application.Users.FinalizeAccountDeletion;

internal sealed class FinalizeAccountDeletionCommandHandler : ICommandHandler<FinalizeAccountDeletionCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public FinalizeAccountDeletionCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(FinalizeAccountDeletionCommand request, CancellationToken cancellationToken)
    {
        User? user = await _userRepository.GetByIdIgnoringFiltersAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Success();
        }

        if (user.Status != UserStatus.PendingDeletion)
        {
            return Result.Success();
        }

        user.Delete();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
