using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Users.GetLoggedInUser;
using Bookify.Domain.Abstractions;
using Bookify.Infrastructure.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NETCore.Keycloak.Client.HttpClients.Abstraction;
using NETCore.Keycloak.Client.Models;
using NETCore.Keycloak.Client.Models.Auth;
using NETCore.Keycloak.Client.Models.Common;
using NETCore.Keycloak.Client.Models.Users;

namespace Bookify.Infrastructure.Identity;

internal sealed class KeycloakIdentityProvider : IIdentityProvider
{
    private readonly IKeycloakClientFactory _clientFactory;
    private readonly KeycloakOptions _options;
    private readonly ILogger<KeycloakIdentityProvider> _logger;

    private static readonly Error UserDeletionFailed = new(
        "Keycloak.UserDeletionFailed",
        "Failed to delete the user");

    private static readonly Error UserCreationFailed = new(
        "Keycloak.UserCreationFailed",
        "Failed to create the user");

    public KeycloakIdentityProvider(
        IKeycloakClientFactory clientFactory,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakIdentityProvider> logger)
    {
        _clientFactory = clientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<string>> CreateUserAsync(
        string email,
        string firstName,
        string lastName,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IKeycloakClient client = await _clientFactory.GetClientAsync();
            string? token = await _clientFactory.GetAccessToken();
            string realm = _clientFactory.GetRealm();

            var kcUser = new KcUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Enabled = true,
                EmailVerified = true
            };

            KcResponse<object> response = await client.Users.CreateAsync(realm, token, kcUser, cancellationToken);

            if (response.IsError)
            {
                _logger.LogWarning(
                    response.Exception
                    , "Failed to create user {Email} in identity provider. Error: {Error}",
                    email,
                    response.ErrorMessage);

                return Result.Failure<string>(UserCreationFailed);
            }

            // Retrieve the 
            UserResponse? userInfo = await GetUserByEmailAsync(email, cancellationToken);

            if (userInfo is null)
            {
                _logger.LogWarning("After creation, user {Email} could not be found in identity provider", email);
                return Result.Failure<string>(UserCreationFailed);
            }

            Guid userId = userInfo.Id;

            var credential = new KcCredentials
            {
                Type = "password",
                Value = password,
                Temporary = false
            };

            KcResponse<object>? passwordResponse = await client.Users.ResetPasswordAsync(realm, token, userId.ToString(), credential, cancellationToken);

            if (!passwordResponse.IsError)
            {
                return Result.Success(userId.ToString());
            }

            _logger.LogWarning(passwordResponse.Exception,
                "Failed to set password for user {UserId}/{Email} in identity provider. Deleting user",
                userId, email);

            // Cleanup - delete the user if password setting fails
            await client.Users.DeleteAsync(realm, token, userId.ToString(), cancellationToken);

            return Result.Failure<string>(new Error(
                "Keycloak.PasswordSetFailed",
                "Failed to set password in identity provider"));

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Email} in Keycloak", email);
            return Result.Failure<string>(new Error(
                "Keycloak.UnexpectedError",
                "An unexpected error occurred while creating the user"));
        }
    }

    public async Task<Result> DeleteUserAsync(
        string identityId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IKeycloakClient client = await _clientFactory.GetClientAsync();
            string? token = await _clientFactory.GetAccessToken();
            string realm = _clientFactory.GetRealm();

            KcResponse<object>? response = await client.Users.DeleteAsync(realm, token, identityId, cancellationToken);

            if (!response.IsError)
            {
                return Result.Success();
            }

            _logger.LogWarning(response.Exception,
                "Failed to delete user {IdentityId}. Error: {Error}",
                identityId,
                response.ErrorMessage);

            return Result.Failure(UserDeletionFailed);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {IdentityId} from Keycloak", identityId);
            return Result.Failure(new Error(
                "Keycloak.UnexpectedError",
                "An unexpected error occurred while deleting the user"));
        }
    }

    public async Task<Result> ResetPasswordAsync(
        string identityId,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IKeycloakClient client = await _clientFactory.GetClientAsync();
            string? token = await _clientFactory.GetAccessToken();
            string realm = _clientFactory.GetRealm();

            var credential = new KcCredentials
            {
                Type = "password",
                Value = newPassword,
                Temporary = false
            };

            KcResponse<object> response = await client.Users.ResetPasswordAsync(realm, token, identityId, credential, cancellationToken);

            if (response.IsError)
            {
                _logger.LogWarning(response.Exception,
                    "Failed to reset password for user {IdentityId}. Error: {Error}",
                    identityId,
                    response.ErrorMessage);

                return Result.Failure(new Error(
                    "Keycloak.PasswordResetFailed",
                    "Failed to reset password in identity provider"));
            }

            _logger.LogInformation("Password reset successfully for user {IdentityId}", identityId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {IdentityId}", identityId);
            return Result.Failure(new Error(
                "Keycloak.UnexpectedError",
                "An unexpected error occurred while resetting the password"));
        }
    }

    public async Task<UserResponse?> GetUserByIdentityIdAsync(
        string identityId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IKeycloakClient client = await _clientFactory.GetClientAsync();
            string? token = await _clientFactory.GetAccessToken();
            string realm = _clientFactory.GetRealm();

            KcResponse<KcUser> kcUser = await client.Users.GetAsync(realm, token, identityId, cancellationToken);

            if (!kcUser.IsError && kcUser.Response != null)
            {
                return MapToUserResponseDto(kcUser.Response);
            }

            _logger.LogWarning("User {IdentityId} not found in Keycloak", identityId);
            return null;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user {IdentityId} from Keycloak", identityId);
            return null;
        }
    }

    public async Task<UserResponse?> GetUserByEmailAsync(string email,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IKeycloakClient client = await _clientFactory.GetClientAsync();
            string? token = await _clientFactory.GetAccessToken();
            string realm = _clientFactory.GetRealm();

            var filter = new KcUserFilter
            {
                Email = email,
                Enabled = true,
                Max = 1,
                Exact = true,
                EmailVerified = true
            };

            KcResponse<IEnumerable<KcUser>> existsResponse = await client.Users.ListUserAsync(realm, token, filter, cancellationToken);

            if (existsResponse.IsError)
            {
                _logger.LogWarning(existsResponse.Exception,
                    "Unable to find user with email {Email}. Error: {Error}",
                    email,
                    existsResponse.ErrorMessage);
                return null;
            }

            KcUser? user = existsResponse.Response.FirstOrDefault();

            if (user?.Id != null)
            {
                return MapToUserResponseDto(user);
            }

            _logger.LogWarning("User with email {Email} not found.", email);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving user identity with email {Email} from Keycloak", email);
            return null;
        }
    }

    public async Task<bool> CheckUserByEmailExistsAsync(string email,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IKeycloakClient client = await _clientFactory.GetClientAsync();
            string? token = await _clientFactory.GetAccessToken();
            string realm = _clientFactory.GetRealm();

            KcResponse<bool> existsResponse = await client.Users.IsUserExistsByEmailAsync(realm, token, email, cancellationToken);

            if (!existsResponse.IsError)
            {
                return existsResponse.Response;
            }

            _logger.LogWarning(
                existsResponse.Exception,
                "There's been an error while checking if the user with email {Email} exists. Error: {Error}",
                email,
                existsResponse.ErrorMessage);

            return false;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking existence of user with email {Email} in Keycloak", email);
            return false;
        }

    }

    public async Task<bool> ValidateCredentialsAsync(string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IKeycloakClient client = await _clientFactory.GetClientAsync();
            string realm = _clientFactory.GetRealm();

            KcOperationResponse<bool>? validateResponse = await client.Auth.ValidatePasswordAsync(
                realm,
                new KcClientCredentials
                {
                    ClientId = _options.AuthClientId,
                    Secret = _options.AuthClientSecret
                },
                new KcUserLogin
                {
                    Username = email,
                    Password = password
                }, cancellationToken);

            if (validateResponse.IsError)
            {
                _logger.LogWarning(
                    validateResponse.Exception,
                    "Failed to validate the password for user {Email}. Error: {Error}",
                    email,
                    validateResponse.ErrorMessage);

                return false;
            }

            return validateResponse.Response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking existence of user with email {Email} in Keycloak", email);
            return false;
        }
    }

    private static UserResponse MapToUserResponseDto(KcUser kcUser) =>
        new(
            Guid.Parse(kcUser.Id),
            kcUser.FirstName,
            kcUser.LastName,
            kcUser.Email,
            DateOnly.MinValue,
            null
        );

    public async Task<Result> UpdateUserEmailAsync(string identityId, string newEmail, CancellationToken cancellationToken = default)
    {
        try
        {
            IKeycloakClient client = await _clientFactory.GetClientAsync();
            string? token = await _clientFactory.GetAccessToken();
            string realm = _clientFactory.GetRealm();

            var userUpdate = new KcUser
            {
                Email = newEmail,
                UserName = newEmail,
                EmailVerified = true
            };

            KcResponse<object> response = await client.Users.UpdateAsync(realm, token, identityId, userUpdate, cancellationToken);

            if (!response.IsError)
            {
                return Result.Success();
            }

            _logger.LogWarning(response.Exception,
                "Failed to update email for user {IdentityId}. Error: {Error}",
                identityId,
                response.ErrorMessage);

            return Result.Failure(new Error("Keycloak.EmailUpdateFailed", "Failed to update email in identity provider"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence of user with id {IdentityId} in Keycloak", identityId);
            return Result.Failure(new Error("Keycloak.UnexpectedError", "An unexpected error occurred while updating the user email"));
        }
    }

    public async Task<Result> LogoutAllSessionsAsync(string identityId, CancellationToken cancellationToken = default)
    {
        try
        {
            IKeycloakClient client = await _clientFactory.GetClientAsync();
            string? token = await _clientFactory.GetAccessToken();
            string realm = _clientFactory.GetRealm();

            KcResponse<object> response = await client.Users.LogoutFromAllSessionsAsync(realm, token, identityId, cancellationToken);

            if (!response.IsError)
            {
                return Result.Success();
            }

            _logger.LogWarning(response.Exception,
                "Failed to logout sessions for user {IdentityId}. Error: {Error}",
                identityId,
                response.ErrorMessage);

            return Result.Failure(new Error("Keycloak.LogoutFailed", "Failed to logout user sessions"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging out user {IdentityId} from Keycloak", identityId);
            return Result.Failure(new Error("Keycloak.UnexpectedError", "An unexpected error occurred while logging out the user"));
        }
    }

    public async Task<Result> UpdateUserProfileAsync(string identityId, string firstName, string lastName, CancellationToken cancellationToken = default)
    {
        try
        {
            IKeycloakClient client = await _clientFactory.GetClientAsync();
            string? token = await _clientFactory.GetAccessToken();
            string realm = _clientFactory.GetRealm();

            var userUpdate = new KcUser
            {
                FirstName = firstName,
                LastName = lastName
            };

            KcResponse<object> response = await client.Users.UpdateAsync(realm, token, identityId, userUpdate, cancellationToken);

            if (!response.IsError)
            {
                return Result.Success();
            }

            _logger.LogWarning(response.Exception,
                "Failed to update profile for user {IdentityId}. Error: {Error}",
                identityId,
                response.ErrorMessage);

            return Result.Failure(new Error("Keycloak.ProfileUpdateFailed", "Failed to update profile in identity provider"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for user {IdentityId} in Keycloak", identityId);
            return Result.Failure(new Error("Keycloak.UnexpectedError", "An unexpected error occurred while updating the user profile"));
        }
    }
}
