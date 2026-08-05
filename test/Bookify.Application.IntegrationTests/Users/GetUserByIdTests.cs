using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users.Requests;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.Users.GetUserById;
using Bookify.Domain.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Application.IntegrationTests.Users;

public class GetUserByIdTests : BaseIntegrationTest
{
    public GetUserByIdTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetUserById_ShouldReturnUnauthorized_WhenNoToken()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/users/{Guid.NewGuid()}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserById_ShouldReturnForbidden_WhenUserLacksAdminPermission()
    {
        // Arrange: login as a regular Registered user (no Admin role)
        string accessToken = await GetAccessToken(
            UserData.LoginUserRequest.Email,
            UserData.LoginUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/users/{Guid.NewGuid()}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUserById_ShouldReturnOk_WhenUserHasAdminPermission()
    {
        // Arrange: promote user to Admin via DB
        string adminEmail = UserData.GetUserByIdUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string accessToken = await GetAccessToken(
            adminEmail,
            UserData.GetUserByIdUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Get the user's own ID to query
        User targetUser = await DbContext.Set<User>()
            .AsNoTracking()
            .FirstAsync(u => u.Email == new Email(adminEmail));

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/users/{targetUser.Id}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        AdminUserResponse? userResponse = await response.Content.ReadFromJsonAsync<AdminUserResponse>();
        userResponse.Should().NotBeNull();
        userResponse.Email.Should().Be(adminEmail);
        userResponse.FirstName.Should().Be(UserData.GetUserByIdUserRequest.FirstName);
        userResponse.LastName.Should().Be(UserData.GetUserByIdUserRequest.LastName);
        userResponse.Roles.Should().NotBeEmpty();
        userResponse.StatusCode.Should().Be('A');
        userResponse.StatusName.Should().Be("Active");
        userResponse.BanCount.Should().Be(0);
        userResponse.IdentityId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetUserById_ShouldReturnStripeCustomerId_WhenUserHasOne()
    {
        // Arrange: register a guest user via HTTP API
        string email = $"getuser_stripe_{Guid.NewGuid()}@test.com";
        string password = "Password123!";
        var registerRequest = new RegisterUserRequest(email, "Stripe", "User", password, new DateOnly(1995, 1, 1));
        HttpResponseMessage regResponse =
            await HttpClient.PostAsJsonAsync("api/v1/users/register/guest", registerRequest);
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Wait until outbox processor sets StripeCustomerId
        var timeoutAt = DateTime.UtcNow.AddSeconds(15);
        User? dbTargetUser = null;
        while (DateTime.UtcNow < timeoutAt)
        {
            DbContext.ChangeTracker.Clear();
            dbTargetUser = await DbContext.Set<User>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == new Email(email));
            if (dbTargetUser?.StripeCustomerId is not null)
            {
                break;
            }

            await Task.Delay(100);
        }

        dbTargetUser.Should().NotBeNull();
        dbTargetUser.StripeCustomerId.Should().NotBeNullOrWhiteSpace();

        // Login as admin
        string adminEmail = UserData.GetUserByIdUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);
        string accessToken = await GetAccessToken(adminEmail, UserData.GetUserByIdUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/users/{dbTargetUser.Id}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        AdminUserResponse? userResponse = await response.Content.ReadFromJsonAsync<AdminUserResponse>();
        userResponse.Should().NotBeNull();
        userResponse.StripeCustomerId.Should().Be(dbTargetUser.StripeCustomerId);
    }

    [Fact]
    public async Task GetUserById_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange: promote user to Admin via DB
        string adminEmail = UserData.GetUserByIdUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string accessToken = await GetAccessToken(
            adminEmail,
            UserData.GetUserByIdUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act: query a non-existent user ID
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/users/{Guid.NewGuid()}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
