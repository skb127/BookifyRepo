using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Users;

[Collection("IntegrationTests")]
public class ChangeUserPasswordTests : BaseIntegrationTest
{
    public ChangeUserPasswordTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnOk_WhenRequestIsValid()
    {
        // Arrange
        var user = UserData.ChangePasswordUserRequest;
        string accessToken = await GetAccessToken(user.Email, user.Password);

        var request = new ChangeUserPasswordRequest(
            user.Password,
            "NewPassword123!");

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

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
        var user = UserData.ChangePasswordUserRequest2; // Using another user to avoid interference, though ideally we could reuse if we don't change state
        string accessToken = await GetAccessToken(user.Email, user.Password);

        var request = new ChangeUserPasswordRequest(
            "WrongPassword!",
            "NewPassword123!");

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
