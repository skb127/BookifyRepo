using Bookify.Domain.Users;

namespace Bookify.Application.UnitTests.Users;

internal static class UserData
{
    public static User Create(Role? role = null) =>
        User.Create(FirstName, LastName, Email, DateOfBirth.Create(new DateOnly(2000, 1, 1)), role ?? Role.Guest);

    private static readonly FirstName FirstName = new("Name");
    private static readonly LastName LastName = new("Last");
    private static readonly Email Email = new("test@test.com");
}
