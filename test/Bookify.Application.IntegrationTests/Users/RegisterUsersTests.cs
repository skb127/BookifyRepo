using System.Net;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Application.IntegrationTests.Users;

public class RegisterUsersTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory _factory;

    public RegisterUsersTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    // [Fact(Skip = "This doesn't work at the moment")]
    public async Task Register_ShouldReturnOk_WhenRequestIsValid()
    {
        // Arrange
        var request = new RegisterUserRequest($"user-{Guid.NewGuid()}@test.com", "name", "lastname", "ClaveSegura1$", new DateOnly(2000, 1, 1));

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [ClassData(typeof(InvalidDataRegister))]
    public async Task Register_ShouldReturnBadRequest_WhenRequestIsInvalid(string email,
        string firstName,
        string lastName,
        string password)
    {
        var request = new RegisterUserRequest(email, firstName, lastName, password, new DateOnly(2000, 1, 1));

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_ShouldReturnBadRequest_WhenTurnstileTokenIsMissing()
    {
        // Arrange
        var request = new RegisterUserRequest($"user-{Guid.NewGuid()}@test.com", "name", "lastname", "ClaveSegura1$", new DateOnly(2000, 1, 1));
        HttpClient.DefaultRequestHeaders.Remove("X-Turnstile-Token");

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Missing Turnstile token");
    }

    [Fact]
    public async Task Register_ShouldReturnBadRequest_WhenTurnstileTokenIsInvalid()
    {
        // Arrange
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

        var request = new RegisterUserRequest($"user-{Guid.NewGuid()}@test.com", "name", "lastname", "ClaveSegura1$", new DateOnly(2000, 1, 1));

        // Act
        HttpResponseMessage response = await customClient.PostAsJsonAsync("api/v1/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid Turnstile token");
    }
}
