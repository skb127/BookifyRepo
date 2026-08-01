using Bookify.Application.Abstractions.Identity;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bookify.Application.Users.DeleteUser;

internal sealed class UserDeletedKeycloakSyncHandler : INotificationHandler<UserDeletedDomainEvent>
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly ILogger<UserDeletedKeycloakSyncHandler> _logger;

    public UserDeletedKeycloakSyncHandler(
        IUserRepository userRepository,
        IIdentityProvider identityProvider,
        ILogger<UserDeletedKeycloakSyncHandler> logger)
    {
        _userRepository = userRepository;
        _identityProvider = identityProvider;
        _logger = logger;
    }

    public async Task Handle(UserDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            User? user = await _userRepository.GetByIdIgnoringFiltersAsync(notification.UserId, cancellationToken);

            if (user is null || string.IsNullOrWhiteSpace(user.IdentityId))
            {
                _logger.LogWarning("User {UserId} not found or missing IdentityId for Keycloak deletion.", notification.UserId);
                return;
            }

            await _identityProvider.DeleteUserAsync(user.IdentityId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user {UserId} from Keycloak.", notification.UserId);
        }
    }
}
