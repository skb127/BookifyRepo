using System.Net;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users.Requests;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.Options;
using Bookify.Application.Users;
using Bookify.Domain.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Application.IntegrationTests.Users;

public class UserPasswordTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory _factory;
    private readonly MockEmailService _mockEmailService;

    public UserPasswordTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _factory = factory;
        _mockEmailService = factory.MockEmailService;
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturnOk_WhenEmailIsValid()
    {
        // Arrange
        var user = UserData.PasswordRecoveryUserRequest;
        DateTime since = DateTime.UtcNow;

        // Act
        var request = new PasswordRecoveryRequest(user.Email);
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/forgot-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify email sent
        EmailMessage recoveryEmail = await _mockEmailService.WaitForEmailToAsync(user.Email, since: since);
        recoveryEmail.Subject.Should().Be("Password Recovery Request");
        recoveryEmail.Body.Should().Contain("/users/reset-password/");
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturnOk_WhenEmailDoesNotExist()
    {
        // Arrange
        DateTime since = DateTime.UtcNow;
        string unknownEmail = $"unknown-{Guid.NewGuid()}@test.com";

        // Act
        var request = new PasswordRecoveryRequest(unknownEmail);
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/forgot-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify NO email sent to this address
        await _mockEmailService.EnsureNoEmailToAsync(unknownEmail, since: since);
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturnBadRequest_WhenEmailIsInvalid()
    {
        // Arrange
        string invalidEmail = "invalid-email";

        // Act
        var request = new PasswordRecoveryRequest(invalidEmail);
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/forgot-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_ShouldSucceed_AndAllowLoginWithNewPassword_WhenFlowIsCorrect()
    {
        // Arrange
        var user = UserData.PasswordResetUserRequest;
        DateTime since = DateTime.UtcNow;

        await HttpClient.PostAsJsonAsync("api/v1/users/forgot-password", new PasswordRecoveryRequest(user.Email));

        EmailMessage recoveryEmail = await _mockEmailService.WaitForEmailToAsync(user.Email, since: since);
        string token = EmailTestUtils.ExtractToken(recoveryEmail.Body, "reset-password");

        string newPassword = "NewPassword123!";

        // Act - Reset Password
        var resetRequest = new PasswordResetRequest(token, newPassword);
        HttpResponseMessage resetResponse = await HttpClient.PostAsJsonAsync("api/v1/users/reset-password", resetRequest);

        // Assert - Reset Success
        resetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Login with New Password
        var loginResponse = await HttpClient.PostAsJsonAsync("api/v1/users/login", new LoginUserRequest(user.Email, newPassword));

        // Assert - Login Success
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AccessTokenOnlyResponse>();
        loginResult.Should().NotBeNull();
        loginResult.AccessToken.Should().NotBeNullOrEmpty();

        // Act - Login with Old Password
        var oldLoginResponse = await HttpClient.PostAsJsonAsync("api/v1/users/login", new LoginUserRequest(user.Email, user.Password));
        oldLoginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenTokenIsInvalid()
    {
        // Arrange
        string invalidToken = "invalid-token";
        string newPassword = "NewPassword123!";

        // Act
        var resetRequest = new PasswordResetRequest(invalidToken, newPassword);
        HttpResponseMessage resetResponse = await HttpClient.PostAsJsonAsync("api/v1/users/reset-password", resetRequest);

        // Assert
        resetResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await resetResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Title.Should().Be(UserErrors.InvalidToken.Code);
    }

    [Fact]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenTokenIsExpired()
    {
        // Arrange - Custom factory with short expiration
        // NOTE: New factory instance = new containers = empty DB. We must register a user here.
        await using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<ExpirationOptions>(options =>
                {
                    options.PasswordRecoveryExpirationSeconds = -5; // Expired immediately
                });
                services.Configure<Bookify.Infrastructure.Security.TurnstileOptions>(options =>
                {
                    options.BaseUrl = new Uri("https://challenges.cloudflare.com/turnstile/v0/");
                    options.SecretKey = "1x0000000000000000000000000000000AA";
                });
            });
        });

        var customClient = customFactory.CreateClient();
        customClient.DefaultRequestHeaders.Add("X-Turnstile-Token", "XXXX.DUMMY.TOKEN.XXXX");
        var customEmailService = customFactory.Services.GetRequiredService<IEmailService>() as MockEmailService;

        // Create a unique user for this isolated environment
        var user = new RegisterUserRequest(
            $"expired-{Guid.NewGuid()}@test.com",
            "Expired",
            "User",
            "Pass123!",
            new DateOnly(2000, 1, 1)
        );

        // Register using custom client
        var response = await customClient.PostAsJsonAsync("api/v1/users/register/guest", user);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"RegisterUser failed: {await response.Content.ReadAsStringAsync()}");
        }

        customEmailService!.Clear();

        // Act - Forgot Password
        await customClient.PostAsJsonAsync("api/v1/users/forgot-password", new PasswordRecoveryRequest(user.Email));

        EmailMessage recoveryEmail = await customEmailService.WaitForEmailToAsync(user.Email, "Password Recovery Request");
        string token = EmailTestUtils.ExtractToken(recoveryEmail.Body, "reset-password");

        // Act - Reset Password
        var resetRequest = new PasswordResetRequest(token, "NewPassword123!");
        HttpResponseMessage resetResponse = await customClient.PostAsJsonAsync("api/v1/users/reset-password", resetRequest);

        // Assert
        resetResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await resetResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Title.Should().Be(UserErrors.InvalidToken.Code);
    }

    [Fact]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenNewPasswordIsWeak()
    {
        // Arrange
        string token = "any-token";
        string weakPassword = "123";

        // Act
        var resetRequest = new PasswordResetRequest(token, weakPassword);
        HttpResponseMessage resetResponse = await HttpClient.PostAsJsonAsync("api/v1/users/reset-password", resetRequest);

        // Assert
        resetResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenTokenIsEmpty()
    {
        // Arrange
        string emptyToken = "";
        string newPassword = "NewPassword123!";

        // Act
        var resetRequest = new PasswordResetRequest(emptyToken, newPassword);
        HttpResponseMessage resetResponse = await HttpClient.PostAsJsonAsync("api/v1/users/reset-password", resetRequest);

        // Assert
        resetResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await resetResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Title.Should().Be("Validation error");
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturnBadRequest_WhenTurnstileTokenIsMissing()
    {
        // Arrange
        var user = UserData.PasswordRecoveryUserRequest;
        HttpClient.DefaultRequestHeaders.Remove("X-Turnstile-Token");

        // Act
        var request = new PasswordRecoveryRequest(user.Email);
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/forgot-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Missing Turnstile token");
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturnBadRequest_WhenTurnstileTokenIsInvalid()
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
        var user = UserData.PasswordRecoveryUserRequest;

        // Act
        var request = new PasswordRecoveryRequest(user.Email);
        HttpResponseMessage response = await customClient.PostAsJsonAsync("api/v1/users/forgot-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid Turnstile token");
    }

    [Fact]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenTurnstileTokenIsMissing()
    {
        // Arrange
        var resetRequest = new PasswordResetRequest("dummy-token", "NewPassword123!");
        HttpClient.DefaultRequestHeaders.Remove("X-Turnstile-Token");

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/reset-password", resetRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Missing Turnstile token");
    }

    [Fact]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenTurnstileTokenIsInvalid()
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

        var resetRequest = new PasswordResetRequest("dummy-token", "NewPassword123!");

        // Act
        HttpResponseMessage response = await customClient.PostAsJsonAsync("api/v1/users/reset-password", resetRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid Turnstile token");
    }
}
