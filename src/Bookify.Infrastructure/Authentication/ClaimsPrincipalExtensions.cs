using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Bookify.Infrastructure.Authentication;

internal static class ClaimsPrincipalExtensions
{
    public static string GetIdentityId(this ClaimsPrincipal? principal) =>
        principal?.FindFirstValue(ClaimTypes.NameIdentifier) ??
        throw new InvalidOperationException("User identity is unavailable");

    public static Guid GetUserId(this ClaimsPrincipal? principal)
    {
        string? userId = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(userId, out Guid parsedUserId) ? parsedUserId :
               throw new InvalidOperationException("User ID is unavailable");
    }
    
    public static string GetEmail(this ClaimsPrincipal? principal) =>
        principal?.FindFirstValue(ClaimTypes.Email) ??
        throw new InvalidOperationException("User email is unavailable");
}
