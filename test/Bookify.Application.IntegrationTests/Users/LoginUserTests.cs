using System.Net;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Bookify.Application.IntegrationTests.Users;

[Collection("IntegrationTests")]
public class LoginUserTests : BaseIntegrationTest
{
    public LoginUserTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Login_ShouldReturnOk_WhenUserDoesExists()
    {
        var request = new LoginUserRequest(UserData.LoginUserRequest.Email, 
            UserData.LoginUserRequest.Password);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // A refreshToken cookie has been set
        response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? setCookies)
            .Should().BeTrue();

        setCookies!.Should().Contain(c => c.Contains("refreshToken="));
    }
    
    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenUserDoesNotExist()
    {
        // Arrange
        var request = new LoginUserRequest("doesnotexist@test.com", "RandomPass123!");

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
