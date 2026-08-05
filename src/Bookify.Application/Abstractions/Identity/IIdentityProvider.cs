using Bookify.Application.Users.GetLoggedInUser;
using Bookify.Domain.Abstractions;

namespace Bookify.Application.Abstractions.Identity;

public interface IIdentityProvider
{
    Task<Result<string>> CreateUserAsync(
        string email,
        string firstName,
        string lastName,
        string password,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteUserAsync(
        string identityId,
        CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(
        string identityId,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<UserResponse?> GetUserByIdentityIdAsync(
        string identityId,
        CancellationToken cancellationToken = default);

    Task<UserResponse?> GetUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<bool> CheckUserByEmailExistsAsync(string email,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateCredentialsAsync(string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<Result> UpdateUserEmailAsync(string identityId, string newEmail, CancellationToken cancellationToken = default);

    Task<Result> LogoutAllSessionsAsync(string identityId, CancellationToken cancellationToken = default);

    Task<Result> UpdateUserProfileAsync(string identityId, string firstName, string lastName, CancellationToken cancellationToken = default);

    Task<Result> DisableUserAsync(string identityId, CancellationToken cancellationToken = default);

    Task<Result> EnableUserAsync(string identityId, CancellationToken cancellationToken = default);
}
