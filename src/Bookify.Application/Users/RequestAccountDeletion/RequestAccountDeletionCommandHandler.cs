using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Abstractions.Scheduling;
using Bookify.Application.Options;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Domain.Shared;
using Bookify.Domain.Users;
using Microsoft.Extensions.Options;

namespace Bookify.Application.Users.RequestAccountDeletion;

internal sealed class RequestAccountDeletionCommandHandler : ICommandHandler<RequestAccountDeletionCommand>
{
    private readonly IUserContext _userContext;
    private readonly IUserRepository _userRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJobScheduler _jobScheduler;
    private readonly IIdentityProvider _identityProvider;
    private readonly AccountDeletionOptions _options;

    public RequestAccountDeletionCommandHandler(
        IUserContext userContext,
        IUserRepository userRepository,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork,
        IJobScheduler jobScheduler,
        IIdentityProvider identityProvider,
        IOptions<AccountDeletionOptions> options)
    {
        _userContext = userContext;
        _userRepository = userRepository;
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _jobScheduler = jobScheduler;
        _identityProvider = identityProvider;
        _options = options.Value;
    }

    public async Task<Result> Handle(RequestAccountDeletionCommand request, CancellationToken cancellationToken)
    {
        User? user = await _userRepository.GetByIdWithRolesAsync(_userContext.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        if (user.Roles.Any(r => r.Id == Role.Admin.Id))
        {
            return Result.Failure(UserErrors.RequestFailed);
        }

        bool hasActiveGuestBookings = await _bookingRepository.HasActiveBookingsAsGuestAsync(user.Id, cancellationToken);
        if (hasActiveGuestBookings)
        {
            return Result.Failure(UserErrors.HasActiveBookingsAsGuest);
        }

        if (user.Roles.Any(r => r.Id == Role.Host.Id))
        {
            bool hasActiveHostBookings = await _bookingRepository.HasActiveBookingsAsHostAsync(user.Id, cancellationToken);
            if (hasActiveHostBookings)
            {
                return Result.Failure(UserErrors.HasActiveBookingsAsHost);
            }
        }

        string rawToken = SecurityUtils.GenerateSecureToken();
        string tokenHash = SecurityUtils.ComputeSha256Hash(rawToken);

        user.RequestDeletion(rawToken, tokenHash, _options.GracePeriodHours);

        await _jobScheduler.ScheduleAccountDeletionAsync(user.Id, user.DeletionScheduledAt!.Value, cancellationToken);

        await _identityProvider.LogoutAllSessionsAsync(user.IdentityId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
