using System.Net;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users.Requests;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Application.IntegrationTests.Users;

public class RegisterGuestTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory _factory;

    public RegisterGuestTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterGuest_ShouldReturnOk_WhenRequestIsValid()
    {
        // Arrange
        DateTime since = DateTime.UtcNow;
        var request = new RegisterUserRequest($"guest-{Guid.NewGuid()}@test.com", "name", "lastname", "ClaveSegura1$", new DateOnly(2000, 1, 1));

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register/guest", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify welcome email was sent via UserCreatedDomainEvent outbox processing
        var welcomeEmail = await _factory.MockEmailService.WaitForEmailToAsync(request.Email, "Welcome to Bookify", since: since);
        welcomeEmail.Should().NotBeNull();
        welcomeEmail.Body.Should().Contain(request.FirstName);
    }

    [Theory]
    [ClassData(typeof(InvalidDataRegister))]
    public async Task RegisterGuest_ShouldReturnBadRequest_WhenRequestIsInvalid(string email,
        string firstName,
        string lastName,
        string password)
    {
        var request = new RegisterUserRequest(email, firstName, lastName, password, new DateOnly(2000, 1, 1));

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register/guest", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterGuest_ShouldReturnBadRequest_WhenTurnstileTokenIsMissing()
    {
        // Arrange
        var request = new RegisterUserRequest($"guest-{Guid.NewGuid()}@test.com", "name", "lastname", "ClaveSegura1$", new DateOnly(2000, 1, 1));
        HttpClient.DefaultRequestHeaders.Remove("X-Turnstile-Token");

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register/guest", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Missing Turnstile token");
    }

    [Fact]
    public async Task RegisterGuest_ShouldReturnBadRequest_WhenTurnstileTokenIsInvalid()
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

        var request = new RegisterUserRequest($"guest-{Guid.NewGuid()}@test.com", "name", "lastname", "ClaveSegura1$", new DateOnly(2000, 1, 1));

        // Act
        HttpResponseMessage response = await customClient.PostAsJsonAsync("api/v1/users/register/guest", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid Turnstile token");
    }

    [Fact]
    public async Task RegisterGuest_ShouldSucceed_AfterAccountDeletion()
    {
        // Arrange
        DateTime sinceInitial = DateTime.UtcNow;
        string email = $"guest_reregister_{Guid.NewGuid()}@test.com";
        var request = new RegisterUserRequest(email, "Guest", "User", "ClaveSegura1$", new DateOnly(2000, 1, 1));

        // 1. Initial registration
        HttpResponseMessage registerResponse = await HttpClient.PostAsJsonAsync("api/v1/users/register/guest", request);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Guid userId = await registerResponse.Content.ReadFromJsonAsync<Guid>();

        // Verify initial welcome email sent
        var initialWelcomeEmail = await _factory.MockEmailService.WaitForEmailToAsync(email, "Welcome to Bookify", since: sinceInitial);
        initialWelcomeEmail.Should().NotBeNull();

        // 2. Admin deletes user (soft delete + Keycloak sync)
        string adminToken = await GetAdminTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        HttpResponseMessage deleteResponse = await HttpClient.DeleteAsync(new Uri($"api/v1/users/{userId}", UriKind.Relative));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Allow outbox processing (1s interval in IntegrationTestWebAppFactory) to sync deletion with Keycloak
        await Task.Delay(1200);

        // 3. Reset authorization header
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // 4. Re-register with the same email
        DateTime sinceReRegister = DateTime.UtcNow;
        HttpResponseMessage reRegisterResponse = await HttpClient.PostAsJsonAsync("api/v1/users/register/guest", request);

        // Assert
        reRegisterResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify welcome email was sent again on re-registration
        var reRegisterWelcomeEmail = await _factory.MockEmailService.WaitForEmailToAsync(email, "Welcome to Bookify", since: sinceReRegister);
        reRegisterWelcomeEmail.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterGuest_ShouldFail_WhenEmailExistsAndIsActive()
    {
        // Arrange
        string email = $"guest_active_dup_{Guid.NewGuid()}@test.com";
        var request = new RegisterUserRequest(email, "Guest", "User", "ClaveSegura1$", new DateOnly(2000, 1, 1));

        // 1. Initial registration succeeds
        HttpResponseMessage registerResponse = await HttpClient.PostAsJsonAsync("api/v1/users/register/guest", request);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Duplicate registration attempt without deleting
        HttpResponseMessage duplicateResponse = await HttpClient.PostAsJsonAsync("api/v1/users/register/guest", request);

        // Assert
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
