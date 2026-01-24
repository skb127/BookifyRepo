using Bookify.Api.Controllers.Users;

namespace Bookify.Api.FunctionalTests.Users;

internal static class UserData
{
    public static readonly RegisterUserRequest RegisterTestUserRequest = new("test@test.com", "name", "lastname", "ClaveSegura1?");
    public static readonly RegisterUserRequest RegisterTestUserRequest2 = new("test2@test.com", "name", "lastname", "ClaveSegura12?");
    public static readonly RegisterUserRequest RegisterTestUserRequest3 = new("test3@test.com", "name", "lastname", "ClaveSegura23?");
}
