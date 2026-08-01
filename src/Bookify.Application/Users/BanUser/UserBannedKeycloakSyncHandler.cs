using Bookify.Application.Abstractions.Identity;
using Bookify.Domain.Users.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bookify.Application.Users.BanUser;

internal sealed class UserBannedKeycloakSyncHandler : INotificationHandler<UserBannedDomainEvent>
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ILogger<UserBannedKeycloakSyncHandler> _logger;

    public UserBannedKeycloakSyncHandler(
        IIdentityProvider identityProvider,
        ILogger<UserBannedKeycloakSyncHandler> logger)
    {
        _identityProvider = identityProvider;
        _logger = logger;
    }

    public async Task Handle(UserBannedDomainEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(notification.IdentityId))
            {
                _logger.LogWarning("UserBannedDomainEvent for user {UserId} is missing IdentityId.", notification.UserId);
                return;
            }

            await _identityProvider.DisableUserAsync(notification.IdentityId, cancellationToken);
            await _identityProvider.LogoutAllSessionsAsync(notification.IdentityId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable Keycloak user {IdentityId} for user {UserId}.", notification.IdentityId, notification.UserId);
        }
    }
}
