using Bookify.Application.Abstractions.Payments;
using Bookify.Domain.Users.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bookify.Application.Users.DeleteUser;

/// <summary>
/// Synchronizes user deletion with Stripe by deactivating the customer.
/// Note: This handler is ready and functional, but will not be triggered
/// until user deletion logic (e.g. User.Delete()) is implemented in a future phase.
/// </summary>
internal sealed class UserDeletedStripeCustomerSyncHandler : INotificationHandler<UserDeletedDomainEvent>
{
    private readonly IStripeCustomerService _stripeCustomerService;
    private readonly ILogger<UserDeletedStripeCustomerSyncHandler> _logger;

    public UserDeletedStripeCustomerSyncHandler(
        IStripeCustomerService stripeCustomerService,
        ILogger<UserDeletedStripeCustomerSyncHandler> logger)
    {
        _stripeCustomerService = stripeCustomerService;
        _logger = logger;
    }

    public async Task Handle(UserDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(notification.StripeCustomerId))
            {
                _logger.LogWarning("UserDeletedDomainEvent for user {UserId} does not contain a Stripe Customer ID.", notification.UserId);
                return;
            }

            await _stripeCustomerService.DeactivateCustomerAsync(notification.StripeCustomerId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate Stripe customer {StripeCustomerId} for user {UserId}.", notification.StripeCustomerId, notification.UserId);
        }
    }
}
