using Bookify.Application.Users;

namespace Bookify.Application.Abstractions.Authorization;

public interface IAuthorizationService
{
    Task<UserRolesResponse> GetRolesForUserAsync(string identityId);
    Task<HashSet<string>> GetPermissionsForUserAsync(string identityId);
}
