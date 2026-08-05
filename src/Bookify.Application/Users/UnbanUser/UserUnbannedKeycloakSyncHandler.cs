using Bookify.Application.Abstractions.Identity;
using Bookify.Domain.Users.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bookify.Application.Users.UnbanUser;

internal sealed class UserUnbannedKeycloakSyncHandler : INotificationHandler<UserUnbannedDomainEvent>
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ILogger<UserUnbannedKeycloakSyncHandler> _logger;

    public UserUnbannedKeycloakSyncHandler(
        IIdentityProvider identityProvider,
        ILogger<UserUnbannedKeycloakSyncHandler> logger)
    {
        _identityProvider = identityProvider;
        _logger = logger;
    }

    public async Task Handle(UserUnbannedDomainEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(notification.IdentityId))
            {
                _logger.LogWarning("UserUnbannedDomainEvent for user {UserId} is missing IdentityId.", notification.UserId);
                return;
            }

            await _identityProvider.EnableUserAsync(notification.IdentityId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable Keycloak user {IdentityId} for user {UserId}.", notification.IdentityId, notification.UserId);
        }
    }
}
