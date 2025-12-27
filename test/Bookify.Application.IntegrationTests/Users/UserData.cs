using Bookify.Api.Controllers.Users;

namespace Bookify.Application.IntegrationTests.Users;

internal static class UserData
{
    public static readonly RegisterUserRequest RegisterTestUserRequest = new("test@test.com", "name", "lastname", "123456");
    public static readonly RegisterUserRequest RegisterTestUserRequest2 = new("test2@test.com", "name2", "lastname", "123456");
    public static readonly RegisterUserRequest RegisterTestUserRequest3 = new("test3@test.com", "name1", "lastname", "123456");
}
