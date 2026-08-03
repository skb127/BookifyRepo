using System.Net;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users.Requests;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Application.IntegrationTests.Users;

public class RegisterHostTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory _factory;

    public RegisterHostTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterHost_ShouldReturnOk_WhenRequestIsValid()
    {
        // Arrange
        var request = new RegisterHostRequest($"host-{Guid.NewGuid()}@test.com", "Host", "User", "ClaveSegura1$", new DateOnly(1990, 1, 1), "+34612345678");

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register/host", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [ClassData(typeof(InvalidDataRegister))]
    public async Task RegisterHost_ShouldReturnBadRequest_WhenRequestIsInvalid(string email,
        string firstName,
        string lastName,
        string password)
    {
        var request = new RegisterHostRequest(email, firstName, lastName, password, new DateOnly(1990, 1, 1), "+34612345678");

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register/host", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterHost_ShouldReturnBadRequest_WhenTurnstileTokenIsMissing()
    {
        // Arrange
        var request = new RegisterHostRequest($"host-{Guid.NewGuid()}@test.com", "Host", "User", "ClaveSegura1$", new DateOnly(1990, 1, 1), "+34612345678");
        HttpClient.DefaultRequestHeaders.Remove("X-Turnstile-Token");

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register/host", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Missing Turnstile token");
    }

    [Fact]
    public async Task RegisterHost_ShouldReturnBadRequest_WhenTurnstileTokenIsInvalid()
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

        var request = new RegisterHostRequest($"host-{Guid.NewGuid()}@test.com", "Host", "User", "ClaveSegura1$", new DateOnly(1990, 1, 1), "+34612345678");

        // Act
        HttpResponseMessage response = await customClient.PostAsJsonAsync("api/v1/users/register/host", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid Turnstile token");
    }
}
