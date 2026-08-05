using Bookify.Domain.Shared;
using Bookify.Domain.Users;

namespace Bookify.Domain.UnitTests.Users;

// Helper class for user data
internal static class UserData
{
    public static readonly FirstName FirstName = new("First");
    public static readonly LastName LastName = new("Last");
    public static readonly Email Email = new("test@test.com");
    public static readonly DateOfBirth DateOfBirth = DateOfBirth.Create(new DateOnly(2000, 1, 1));
    public static readonly PhoneNumber PhoneNumber = PhoneNumber.Create("+15555555555")!;
    public static readonly Role DefaultRole = Role.Guest;

    public static User CreateUser(Role? role = null) =>
        User.Create(FirstName, LastName, Email, DateOfBirth, role ?? DefaultRole);
}
