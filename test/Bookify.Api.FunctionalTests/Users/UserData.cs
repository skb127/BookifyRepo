using Bookify.Api.Controllers.Users;

namespace Bookify.Api.FunctionalTests.Users;

internal static class UserData
{
    public static readonly RegisterUserRequest RegisterTestUserRequest = new("test@test.com", "name", "lastname", "123456");
    public static readonly RegisterUserRequest RegisterTestUserRequest2 = new("test2@test.com", "name", "lastname", "123456");
}
