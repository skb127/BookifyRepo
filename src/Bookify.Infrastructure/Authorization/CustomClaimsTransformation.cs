using System.Security.Claims;
using Bookify.Application.Abstractions.Authorization;
using Bookify.Application.Users;
using Bookify.Domain.Users;
using Bookify.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Bookify.Infrastructure.Authorization;

internal sealed class CustomClaimsTransformation : IClaimsTransformation
{
    private readonly IServiceProvider _serviceProvider;

    public CustomClaimsTransformation(IServiceProvider serviceProvider) =>
        _serviceProvider = serviceProvider;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.HasClaim(claim => claim.Type == ClaimTypes.Role) &&
            principal.HasClaim(claim => claim.Type == JwtRegisteredClaimNames.Sub))
        {
            return principal;
        }

        using IServiceScope scope = _serviceProvider.CreateScope();

        IAuthorizationService authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        string identityId = principal.GetIdentityId();

        // Fetch roles from the database
        UserRolesResponse userRoles = await authorizationService.GetRolesForUserAsync(identityId);

        // Create a new ClaimsIdentity to hold the role claims
        var claimsIdentity = new ClaimsIdentity();

        // Add the subject claim
        claimsIdentity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, userRoles.Id.ToString()));

        // Add role claims
        foreach (Role role in userRoles.Roles)
        {
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role.Name));
        }

        // Add the new ClaimsIdentity to the existing principal
        principal.AddIdentity(claimsIdentity);

        return principal;
    }
}
