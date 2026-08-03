using System.Net;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users.Requests;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Application.IntegrationTests.Users;

public class LoginUserTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory _factory;

    public LoginUserTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _factory = factory;
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

    [Fact]
    public async Task Login_ShouldReturnBadRequest_WhenTurnstileTokenIsMissing()
    {
        // Arrange
        var request = new LoginUserRequest(UserData.LoginUserRequest.Email,
            UserData.LoginUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Remove("X-Turnstile-Token");

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Missing Turnstile token");
    }

    [Fact]
    public async Task Login_ShouldReturnBadRequest_WhenTurnstileTokenIsInvalid()
    {
        // Arrange
        // The always-pass test key always passes. To test invalid tokens, we must use the always-fails test key.
        await using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Configure<Bookify.Infrastructure.Security.TurnstileOptions>(options =>
                {
                    options.BaseUrl = new Uri("https://challenges.cloudflare.com/turnstile/v0/");
                    options.SecretKey = "2x0000000000000000000000000000000AA"; // Always-fails key
                });
            });
        });
        
        var customClient = customFactory.CreateClient();
        customClient.DefaultRequestHeaders.Add("X-Turnstile-Token", "invalid-token-string");

        var request = new LoginUserRequest($"login-{Guid.NewGuid()}@test.com", "pass123!");

        // Act
        HttpResponseMessage response = await customClient.PostAsJsonAsync("api/v1/users/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid Turnstile token");
    }
}
