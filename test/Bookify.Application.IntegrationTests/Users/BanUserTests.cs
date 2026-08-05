using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.Users;
using Bookify.Application.Users.GetUserById;
using Bookify.Application.Users.RegisterGuest;
using Bookify.Domain.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Users;

public class BanUserTests : BaseIntegrationTest
{
    public BanUserTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Ban_ShouldSuspendUser_WhenAdminCallsEndpoint()
    {
        // Arrange: Promote admin
        string adminEmail = UserData.GetUserByIdUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string adminToken = await GetAccessToken(adminEmail, UserData.GetUserByIdUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminToken);

        // Register a fresh target user
        string targetEmail = $"target_{Guid.CreateVersion7()}@test.com";
        var registerCommand =
            new RegisterGuestCommand(targetEmail, "Target", "User", "Password123!", new DateOnly(1995, 1, 1));
        Guid targetUserId = (await Sender.Send(registerCommand)).Value;

        // Act: POST /api/v1/users/{id}/ban
        HttpResponseMessage response = await HttpClient.PostAsync(
            new Uri($"api/v1/users/{targetUserId}/ban", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify status via HTTP API
        HttpResponseMessage getResponse =
            await HttpClient.GetAsync(new Uri($"api/v1/users/{targetUserId}", UriKind.Relative));
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var userResponse = await getResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
        userResponse.Should().NotBeNull();
        userResponse.StatusName.Should().Be("Suspended");
        userResponse.BanCount.Should().Be(1);

        // Allow outbox processing (1s interval in IntegrationTestWebAppFactory) to sync ban with Keycloak
        await Task.Delay(1200);

        // Verify target user cannot log in while banned
        HttpResponseMessage loginResponse = await HttpClient.PostAsJsonAsync(
            "api/v1/users/login",
            new Bookify.Api.Controllers.Users.Requests.LoginUserRequest(targetEmail, "Password123!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unban_ShouldRestoreActiveStatus()
    {
        // Arrange: Promote admin
        string adminEmail = UserData.GetUserByIdUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string adminToken = await GetAccessToken(adminEmail, UserData.GetUserByIdUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminToken);

        // Register a fresh target user
        string targetEmail = $"target_unban_{Guid.CreateVersion7()}@test.com";
        var registerCommand =
            new RegisterGuestCommand(targetEmail, "Target", "User", "Password123!", new DateOnly(1995, 1, 1));
        Guid targetUserId = (await Sender.Send(registerCommand)).Value;

        HttpResponseMessage banResponse = await HttpClient.PostAsync(
            new Uri($"api/v1/users/{targetUserId}/ban", UriKind.Relative),
            null);
        banResponse.EnsureSuccessStatusCode();

        // Act: POST /api/v1/users/{id}/unban
        HttpResponseMessage unbanResponse = await HttpClient.PostAsync(
            new Uri($"api/v1/users/{targetUserId}/unban", UriKind.Relative),
            null);

        // Assert
        unbanResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify status via HTTP API
        HttpResponseMessage getResponse =
            await HttpClient.GetAsync(new Uri($"api/v1/users/{targetUserId}", UriKind.Relative));
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var userResponse = await getResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
        userResponse.Should().NotBeNull();
        userResponse.StatusName.Should().Be("Active");
        userResponse.BanCount.Should().Be(1);

        // Allow outbox processing (1s interval in IntegrationTestWebAppFactory) to sync unban with Keycloak
        await Task.Delay(1200);

        // Verify target user can log in after unban
        HttpResponseMessage loginResponse = await HttpClient.PostAsJsonAsync(
            "api/v1/users/login",
            new Bookify.Api.Controllers.Users.Requests.LoginUserRequest(targetEmail, "Password123!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenResponse = await loginResponse.Content.ReadFromJsonAsync<AccessTokenOnlyResponse>();
        tokenResponse.Should().NotBeNull();
        tokenResponse.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Ban_ShouldReturn401_WhenNoToken()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync(
            new Uri($"api/v1/users/{Guid.CreateVersion7()}/ban", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ban_ShouldReturn403_WhenUserIsNotAdmin()
    {
        // Arrange: Register regular guest user
        string guestEmail = $"guest_ban403_{Guid.CreateVersion7()}@test.com";
        var registerCommand =
            new RegisterGuestCommand(guestEmail, "Guest", "User", "Password123!", new DateOnly(1995, 1, 1));
        Guid guestUserId = (await Sender.Send(registerCommand)).Value;

        string guestToken = await GetAccessToken(guestEmail, "Password123!");
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync(
            new Uri($"api/v1/users/{guestUserId}/ban", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Ban_ShouldReturn404_WhenUserDoesNotExist()
    {
        // Arrange
        string adminEmail = UserData.GetUserByIdUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string adminToken = await GetAccessToken(adminEmail, UserData.GetUserByIdUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync(
            new Uri($"api/v1/users/{Guid.CreateVersion7()}/ban", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ban_ShouldReturn400_WhenUserIsAdmin()
    {
        // Arrange: Promote two admin users
        string adminEmail = UserData.GetUserByIdUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string adminToken = await GetAccessToken(adminEmail, UserData.GetUserByIdUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminToken);

        string targetAdminEmail = $"admin_target_{Guid.CreateVersion7()}@test.com";
        var registerCommand = new RegisterGuestCommand(targetAdminEmail, "Target", "Admin", "Password123!",
            new DateOnly(1990, 1, 1));
        Guid targetAdminId = (await Sender.Send(registerCommand)).Value;
        await PromoteToAdminAsync(targetAdminEmail);

        // Act: Try banning an admin
        HttpResponseMessage response = await HttpClient.PostAsync(
            new Uri($"api/v1/users/{targetAdminId}/ban", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Detail.Should().Be(UserErrors.CannotBanAdmin.Name);
    }

    [Fact]
    public async Task Ban_ShouldReturn400_WhenUserIsAlreadySuspended()
    {
        // Arrange
        string adminEmail = UserData.GetUserByIdUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string adminToken = await GetAccessToken(adminEmail, UserData.GetUserByIdUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminToken);

        string targetEmail = $"target_alreadybanned_{Guid.CreateVersion7()}@test.com";
        var registerCommand =
            new RegisterGuestCommand(targetEmail, "Target", "User", "Password123!", new DateOnly(1995, 1, 1));
        Guid targetUserId = (await Sender.Send(registerCommand)).Value;

        // First ban succeeds
        HttpResponseMessage banResponse = await HttpClient.PostAsync(
            new Uri($"api/v1/users/{targetUserId}/ban", UriKind.Relative),
            null);
        banResponse.EnsureSuccessStatusCode();

        // Act: Try banning again
        HttpResponseMessage response = await HttpClient.PostAsync(
            new Uri($"api/v1/users/{targetUserId}/ban", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Detail.Should().Be(UserErrors.AlreadySuspended.Name);
    }

    [Fact]
    public async Task Unban_ShouldReturn400_WhenUserIsNotSuspended()
    {
        // Arrange
        string adminEmail = UserData.GetUserByIdUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string adminToken = await GetAccessToken(adminEmail, UserData.GetUserByIdUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminToken);

        string targetEmail = $"target_activeunban_{Guid.CreateVersion7()}@test.com";
        var registerCommand =
            new RegisterGuestCommand(targetEmail, "Target", "User", "Password123!", new DateOnly(1995, 1, 1));
        Guid targetUserId = (await Sender.Send(registerCommand)).Value;

        // Act: Try unbanning an active user
        HttpResponseMessage response = await HttpClient.PostAsync(
            new Uri($"api/v1/users/{targetUserId}/unban", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Detail.Should().Be(UserErrors.NotSuspended.Name);
    }

    [Fact]
    public async Task Unban_ShouldReturn403_WhenUserIsNotAdmin()
    {
        // Arrange
        string guestEmail = $"guest_unban403_{Guid.CreateVersion7()}@test.com";
        var registerCommand =
            new RegisterGuestCommand(guestEmail, "Guest", "User", "Password123!", new DateOnly(1995, 1, 1));
        Guid guestUserId = (await Sender.Send(registerCommand)).Value;

        string guestToken = await GetAccessToken(guestEmail, "Password123!");
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync(
            new Uri($"api/v1/users/{guestUserId}/unban", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
