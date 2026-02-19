using System.Net;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users;
using Bookify.Api.FunctionalTests.Infrastructure;
using FluentAssertions;

namespace Bookify.Api.FunctionalTests.Users;

public class LoginUserTests : BaseFunctionalTest
{
    private const string Email = "login@test.com";
    private const string Password = "ClaveSegura1?";

    public LoginUserTests(FunctionalTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenUserDoesNotExist()
    {
        // Arrange
        var request = new LoginUserRequest(Email, Password);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ShouldReturnOk_WhenUserDoesExists()
    {
        // Arrange
        var registerRequest = new RegisterUserRequest(Email, "name", "last", Password, new DateOnly(2000, 1, 1));
        await HttpClient.PostAsJsonAsync("api/v1/users/register", registerRequest);

        var request = new LoginUserRequest(Email, Password);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
