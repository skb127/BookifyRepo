using Bookify.Domain.Users;

namespace Bookify.Application.Users;

public sealed class UserRolesResponse
{
    public Guid Id { get; init; }

    public IReadOnlyCollection<Role> Roles { get; init; } = [];
}
