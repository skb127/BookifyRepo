using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.Abstractions.Caching;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.Users.GetLoggedInUser;
using Bookify.Domain.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Application.IntegrationTests.Users;

[Collection("IntegrationTests")]
public class GetUserByIdTests : BaseIntegrationTest
{
    private readonly ICacheService _cacheService;

    public GetUserByIdTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _cacheService = factory.Services.GetRequiredService<ICacheService>();
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

        UserResponse? userResponse = await response.Content.ReadFromJsonAsync<UserResponse>();
        userResponse.Should().NotBeNull();
        userResponse!.Email.Should().Be(adminEmail);
        userResponse.FirstName.Should().Be(UserData.GetUserByIdUserRequest.FirstName);
        userResponse.LastName.Should().Be(UserData.GetUserByIdUserRequest.LastName);
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

    /// <summary>
    /// Assigns the Admin role to the user with the given email by inserting into the join table.
    /// This replicates the manual role assignment that would be done in production.
    /// Also invalidates the Redis-cached roles/permissions so the authorization pipeline re-reads from DB.
    /// </summary>
    private async Task PromoteToAdminAsync(string email)
    {
        User user = await DbContext.Set<User>()
            .AsNoTracking()
            .FirstAsync(u => u.Email == new Email(email))
            .ConfigureAwait(false);

        // Check if the user already has the Admin role to avoid duplicate key errors
        bool alreadyAdmin = await DbContext.Database
            .SqlQuery<int>($"SELECT COUNT(1) AS \"Value\" FROM role_user WHERE roles_id = {Role.Admin.Id} AND users_id = {user.Id}")
            .AnyAsync(c => c > 0)
            .ConfigureAwait(false);

        if (!alreadyAdmin)
        {
            await DbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO role_user (roles_id, users_id) VALUES ({Role.Admin.Id}, {user.Id})")
                .ConfigureAwait(false);
        }

        // Invalidate cached roles/permissions so the authorization pipeline re-reads from DB
        string identityId = user.IdentityId;
        await _cacheService.RemoveAsync($"auth:roles-{identityId}").ConfigureAwait(false);
        await _cacheService.RemoveAsync($"auth:permissions-{identityId}").ConfigureAwait(false);
    }
}
