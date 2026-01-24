using Bookify.Api.Controllers.Users;

namespace Bookify.Application.IntegrationTests.Users;

internal static class UserData
{
    public static readonly RegisterUserRequest RegisterTestUserRequest = new("test@test.com", "name", "lastname", "ClaveSegura1$");
    public static readonly RegisterUserRequest RegisterTestUserRequest2 = new("test2@test.com", "name2", "lastname", "ClaveSegura2$");
    public static readonly RegisterUserRequest RegisterTestUserRequest3 = new("test3@test.com", "name1", "lastname", "ClaveSegura3$");
    public static readonly RegisterUserRequest RegisterChangePasswordUserRequest = new("changepassword@test.com", "Change", "User", "TestPassword123!");
}
