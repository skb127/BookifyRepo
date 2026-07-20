using Bookify.Domain.Abstractions;
using Bookify.Application.Abstractions.Payments;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bookify.Application.Users.RegisterUser;

internal sealed class UserCreatedStripeCustomerSyncHandler : INotificationHandler<UserCreatedDomainEvent>
{
    private readonly IUserRepository _userRepository;
    private readonly IStripeCustomerService _stripeCustomerService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserCreatedStripeCustomerSyncHandler> _logger;

    public UserCreatedStripeCustomerSyncHandler(
        IUserRepository userRepository,
        IStripeCustomerService stripeCustomerService,
        IUnitOfWork unitOfWork,
        ILogger<UserCreatedStripeCustomerSyncHandler> logger)
    {
        _userRepository = userRepository;
        _stripeCustomerService = stripeCustomerService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(UserCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            User? user = await _userRepository.GetByIdAsync(notification.UserId, cancellationToken);

            if (user is null)
            {
                _logger.LogWarning("User with ID {UserId} was not found for Stripe customer creation.", notification.UserId);
                return;
            }

            string stripeCustomerId = await _stripeCustomerService.UpsertCustomerAsync(
                user.Id,
                user.Email.Value,
                $"{user.FirstName.Value} {user.LastName.Value}",
                cancellationToken);

            user.SetStripeCustomerId(stripeCustomerId);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to synchronize created user with ID {UserId} to Stripe.", notification.UserId);
        }
    }
}
