using Bookify.Domain.Users;

namespace Bookify.Application.UnitTests.Users;

internal static class UserData
{
    public static User Create() => User.Create(FirstName, LastName, Email);

    private static readonly FirstName FirstName = new("Name");
    private static readonly LastName LastName = new("Last");
    private static readonly Email Email = new("test@test.com");
}
