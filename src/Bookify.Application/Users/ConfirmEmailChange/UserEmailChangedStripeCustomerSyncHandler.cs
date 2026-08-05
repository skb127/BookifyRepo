using Bookify.Application.Abstractions.Payments;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bookify.Application.Users.ConfirmEmailChange;

internal sealed class UserEmailChangedStripeCustomerSyncHandler : INotificationHandler<UserEmailChangedDomainEvent>
{
    private readonly IUserRepository _userRepository;
    private readonly IStripeCustomerService _stripeCustomerService;
    private readonly ILogger<UserEmailChangedStripeCustomerSyncHandler> _logger;

    public UserEmailChangedStripeCustomerSyncHandler(
        IUserRepository userRepository,
        IStripeCustomerService stripeCustomerService,
        ILogger<UserEmailChangedStripeCustomerSyncHandler> logger)
    {
        _userRepository = userRepository;
        _stripeCustomerService = stripeCustomerService;
        _logger = logger;
    }

    public async Task Handle(UserEmailChangedDomainEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            User? user = await _userRepository.GetByIdAsync(notification.UserId, cancellationToken);

            if (user is null)
            {
                _logger.LogWarning("User with ID {UserId} was not found for Stripe customer email update.", notification.UserId);
                return;
            }

            if (string.IsNullOrWhiteSpace(user.StripeCustomerId))
            {
                _logger.LogWarning("User with ID {UserId} does not have a Stripe Customer ID associated.", notification.UserId);
                return;
            }

            await _stripeCustomerService.UpdateCustomerAsync(
                user.StripeCustomerId,
                notification.NewEmail, // Note: can also use user.Email.Value
                $"{user.FirstName.Value} {user.LastName.Value}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Stripe customer email for user with ID {UserId}.", notification.UserId);
        }
    }
}
