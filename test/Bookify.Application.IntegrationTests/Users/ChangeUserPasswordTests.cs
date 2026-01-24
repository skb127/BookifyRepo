using System.Net;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.Users.ChangeUserPassword;
using Bookify.Application.Users.LoginUser;
using FluentAssertions;

namespace Bookify.Application.IntegrationTests.Users;

public class ChangeUserPasswordTests : BaseIntegrationTest
{
    public ChangeUserPasswordTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnOk_WhenRequestIsValid()
    {
        // Arrange
        var user = UserData.RegisterChangePasswordUserRequest;
        string accessToken = await GetAccessToken(user.Email, user.Password);

        var request = new ChangeUserPasswordRequest(
            user.Password,
            "NewPassword123!");

        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify old password fails
        var loginRequestOld = new LoginUserRequest(user.Email, user.Password);
        HttpResponseMessage loginResponseOld = await HttpClient.PostAsJsonAsync("api/v1/users/login", loginRequestOld);
        loginResponseOld.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Verify  new password works
        var loginRequestNew = new LoginUserRequest(user.Email, "NewPassword123!");
        HttpResponseMessage loginResponseNew = await HttpClient.PostAsJsonAsync("api/v1/users/login", loginRequestNew);
        loginResponseNew.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnBadRequest_WhenCurrentPasswordIsIncorrect()
    {
        // Arrange
        var user = UserData.RegisterTestUserRequest3; // Using another user to avoid interference, though ideally we could reuse if we don't change state
        string accessToken = await GetAccessToken(user.Email, user.Password);

        var request = new ChangeUserPasswordRequest(
            "WrongPassword!",
            "NewPassword123!");

        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
