using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.Options;
using Bookify.Application.Users;
using Bookify.Domain.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Application.IntegrationTests.Users;

public class UserEmailChangeTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory _factory;
    private readonly MockEmailService _mockEmailService;

    public UserEmailChangeTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _factory = factory;
        _mockEmailService = factory.MockEmailService;
    }

    [Fact]
    public async Task ChangeEmail_ShouldSucceed_AndAllowLoginWithNewEmail_WhenFlowIsCorrect()
    {
        // Arrange - Login as the dedicated email change user
        var user = UserData.ChangeEmailUserRequest;
        string accessToken = await GetAccessToken(user.Email, user.Password);
        string newEmail = $"new-{Guid.NewGuid()}@test.com";

        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        DateTime since = DateTime.UtcNow;

        // Act 1 - Initiate email change
        var initiateRequest = new InitiateEmailChangeRequest(newEmail, user.Password);
        HttpResponseMessage initiateResponse = await HttpClient
            .PostAsJsonAsync("api/v1/users/change-email", initiateRequest);

        // Assert - Initiation succeeded
        initiateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Wait for Outbox to process and send emails (Quartz job runs every 1s in tests)
        EmailMessage verificationEmail = await _mockEmailService.WaitForEmailToAsync(newEmail, since: since);
        await _mockEmailService.WaitForEmailToAsync(user.Email, since: since);

        // Assert - Both emails were sent (verification to new email + security alert to current email)
        _mockEmailService.HasEmailTo(user.Email, since).Should().BeTrue("A security alert should be sent to the current email");
        _mockEmailService.HasEmailTo(newEmail, since).Should().BeTrue("A verification email should be sent to the new email");

        verificationEmail.Subject.Should().Be("Confirm Your New Email Address");

        // Extract token from email body URL: /users/confirm-email-change/{token}
        string token = EmailTestUtils.ExtractToken(verificationEmail.Body, "confirm-email-change");
        token.Should().NotBeNullOrEmpty("the verification email must contain a confirmation token");

        // Act 2 - Confirm email change (AllowAnonymous endpoint)
        HttpClient.DefaultRequestHeaders.Authorization = null;
        var confirmRequest = new ConfirmEmailChangeRequest(token);
        HttpResponseMessage confirmResponse = await HttpClient
            .PostAsJsonAsync("api/v1/users/confirm-email-change", confirmRequest);

        // Assert - Confirmation succeeded
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - User can log in with the new email (Get Access Token)
        accessToken = await GetAccessToken(newEmail, user.Password);
        accessToken.Should().NotBeNullOrEmpty("User should be able to login with the new email");
    }

    [Fact]
    public async Task ConfirmEmailChange_ShouldReturnError_WhenTokenIsExpired()
    {
        // Arrange - Create a custom client with short expiration settings
        await using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<ExpirationOptions>(options =>
                {
                    options.EmailChangeExpirationSeconds = -5;
                });
            });
        });

        // Resolve dependencies from the custom factory
        var customClient = customFactory.CreateClient();
        customClient.DefaultRequestHeaders.Add("X-Turnstile-Token", "XXXX.DUMMY.TOKEN.XXXX");
        var customEmailService = customFactory.Services.GetRequiredService<IEmailService>() as MockEmailService;

        // Login as the dedicated user for this test
        var user = UserData.ChangeEmailUserRequest2;

        // Manually login to get access token using custom client
        HttpResponseMessage loginResponse = await customClient.PostAsJsonAsync(
            "api/v1/users/login",
            new LoginUserRequest(user.Email, user.Password));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AccessTokenOnlyResponse>();
        string accessToken = loginResult!.AccessToken;

        customClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        DateTime since = DateTime.UtcNow;

        // Act 1 - Initiate email change
        string newEmail = "expired_test@test.com";
        var initiateRequest = new InitiateEmailChangeRequest(newEmail, user.Password);
        HttpResponseMessage initiateResponse = await customClient
            .PostAsJsonAsync("api/v1/users/change-email", initiateRequest);

        initiateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Wait for Outbox to process and send emails
        EmailMessage verificationEmail = await customEmailService!.WaitForEmailToAsync(newEmail, since: since);
        await customEmailService.WaitForEmailToAsync(user.Email, since: since);

        // Extract token
        string token = EmailTestUtils.ExtractToken(verificationEmail.Body, "confirm-email-change");

        // Act 2 - Try to confirm email change with expired token
        customClient.DefaultRequestHeaders.Authorization = null;
        var confirmRequest = new ConfirmEmailChangeRequest(token);
        HttpResponseMessage confirmResponse = await customClient
            .PostAsJsonAsync("api/v1/users/confirm-email-change", confirmRequest);

        // Assert - Should return BadRequest with explicit error
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest, await confirmResponse.Content.ReadAsStringAsync());

        var problemDetails = await confirmResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(UserErrors.InvalidEmailChangeToken.Code);
        problemDetails.Detail.Should().Be(UserErrors.InvalidEmailChangeToken.Name);
    }

    [Fact]
    public async Task ConfirmEmailChange_ShouldReturnError_WhenTokenIsInvalid()
    {
        // Act
        HttpClient.DefaultRequestHeaders.Authorization = null;
        var confirmRequest = new ConfirmEmailChangeRequest(Guid.NewGuid().ToString());
        HttpResponseMessage confirmResponse = await HttpClient
            .PostAsJsonAsync("api/v1/users/confirm-email-change", confirmRequest);

        // Assert
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await confirmResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(UserErrors.InvalidEmailChangeToken.Code);
        problemDetails.Detail.Should().Be(UserErrors.InvalidEmailChangeToken.Name);
    }

    [Fact]
    public async Task InitiateEmailChange_ShouldReturnError_WhenNewEmailIsAlreadyUsed()
    {
        // Arrange
        var user1 = UserData.RegisterTestUserRequest;
        var user2 = UserData.ExistingUserRequest;

        string accessToken = await GetAccessToken(user1.Email, user1.Password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        var initiateRequest = new InitiateEmailChangeRequest(user2.Email, user1.Password);
        HttpResponseMessage initiateResponse = await HttpClient
            .PostAsJsonAsync("api/v1/users/change-email", initiateRequest);

        // Assert
        initiateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await initiateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(UserErrors.EmailAlreadyInUse.Code);
        problemDetails.Detail.Should().Be(UserErrors.EmailAlreadyInUse.Name);
    }

    [Fact]
    public async Task InitiateEmailChange_ShouldReturnError_WhenNewEmailIsSameAsCurrent()
    {
        // Arrange
        var user = UserData.RegisterTestUserRequest;

        string accessToken = await GetAccessToken(user.Email, user.Password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        var initiateRequest = new InitiateEmailChangeRequest(user.Email, user.Password);
        HttpResponseMessage initiateResponse = await HttpClient
            .PostAsJsonAsync("api/v1/users/change-email", initiateRequest);

        // Assert
        initiateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await initiateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails!.Title.Should().Be("Validation error");
    }

    [Fact]
    public async Task InitiateEmailChange_ShouldReturnError_WhenCurrentPasswordIsInvalid()
    {
        // Arrange
        var user = UserData.RegisterTestUserRequest;

        string accessToken = await GetAccessToken(user.Email, user.Password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        var initiateRequest = new InitiateEmailChangeRequest("newValidEmail@test.com", "WrongPassword123!");
        HttpResponseMessage initiateResponse = await HttpClient
            .PostAsJsonAsync("api/v1/users/change-email", initiateRequest);

        // Assert
        initiateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await initiateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(UserErrors.InvalidCurrentCredentials.Code);
        problemDetails.Detail.Should().Be(UserErrors.InvalidCurrentCredentials.Name);
    }
}
