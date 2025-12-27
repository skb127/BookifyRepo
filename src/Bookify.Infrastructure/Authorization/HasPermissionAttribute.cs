using Microsoft.AspNetCore.Authorization;

namespace Bookify.Infrastructure.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
        : base(permission) =>
        Permission = permission;

    public string Permission { get; }
}
