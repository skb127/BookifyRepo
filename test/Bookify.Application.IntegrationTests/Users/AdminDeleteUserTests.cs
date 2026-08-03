using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.Users.GetUserById;
using Bookify.Application.Users.RegisterGuest;
using Bookify.Domain.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Application.IntegrationTests.Users;

public class AdminDeleteUserTests : BaseIntegrationTest
{
    public AdminDeleteUserTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task AdminDelete_ShouldDeleteUser_WhenAdminDeletesNonAdmin()
    {
        // Arrange: Promote admin
        string adminEmail = UserData.GetUserByIdUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string adminToken = await GetAccessToken(adminEmail, UserData.GetUserByIdUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminToken);

        // Register a fresh target user
        string targetEmail = $"target_delete_{Guid.CreateVersion7()}@test.com";
        var registerCommand =
            new RegisterGuestCommand(targetEmail, "Target", "User", "Password123!", new DateOnly(1995, 1, 1));
        Guid targetUserId = (await Sender.Send(registerCommand)).Value;

        // Allow outbox processing to finish
        await Task.Delay(300);

        // Act: DELETE /api/v1/users/{id}
        HttpResponseMessage response = await HttpClient.DeleteAsync(
            new Uri($"api/v1/users/{targetUserId}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify via HTTP API endpoint GET /api/v1/users/{id}
        HttpResponseMessage getResponse = await HttpClient.GetAsync(
            new Uri($"api/v1/users/{targetUserId}", UriKind.Relative));
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var userResponse = await getResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
        userResponse.Should().NotBeNull();
        userResponse.StatusName.Should().Be("Deleted");
        userResponse.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AdminDelete_ShouldFail_WhenDeletingLastAdmin()
    {
        // Arrange: Promote target user as sole admin
        string singleAdminEmail = $"sole_admin_{Guid.CreateVersion7()}@test.com";
        var registerCommand =
            new RegisterGuestCommand(singleAdminEmail, "Sole", "Admin", "Password123!", new DateOnly(1990, 1, 1));
        Guid singleAdminId = (await Sender.Send(registerCommand)).Value;
        await PromoteToAdminAsync(singleAdminEmail);

        // Remove all other admin roles using EF Core ExecuteDeleteAsync batch delete
        await DbContext.Set<Dictionary<string, object>>("RoleUser")
            .Where(e => (int)e["RolesId"] == Role.Admin.Id && (Guid)e["UsersId"] != singleAdminId)
            .ExecuteDeleteAsync();

        string adminToken = await GetAccessToken(singleAdminEmail, "Password123!");
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminToken);

        // Act: DELETE /api/v1/users/{singleAdminId} (attempt to delete last admin)
        HttpResponseMessage response = await HttpClient.DeleteAsync(
            new Uri($"api/v1/users/{singleAdminId}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(UserErrors.CannotDeleteLastAdmin.Code);
    }
}
