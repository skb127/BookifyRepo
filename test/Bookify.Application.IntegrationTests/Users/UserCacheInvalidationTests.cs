using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.Users.GetLoggedInUser;
using Bookify.Domain.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Application.IntegrationTests.Users;

public class UserCacheInvalidationTests : BaseIntegrationTest
{
    public UserCacheInvalidationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task UpdateUserProfile_ShouldServeUpdatedData_WhenGetUserByIdCacheIsInvalidated()
    {
        // Arrange / Act
        // promote to Admin (GetUserById requires Admin permission)
        string adminEmail = UserData.CacheInvalidationUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string accessToken = await GetAccessToken(adminEmail, UserData.CacheInvalidationUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, accessToken);

        // 1. Resolve the user's own DB id
        User targetUser = await DbContext.Set<User>()
            .AsNoTracking()
            .FirstAsync(u => u.Email == new Email(adminEmail));

        var getUserUri = new Uri($"api/v1/users/{targetUser.Id}", UriKind.Relative);

        // 2. GET the user to populate the cache (cache miss → stored in Redis)
        HttpResponseMessage firstGetResponse = await HttpClient.GetAsync(getUserUri);
        firstGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        UserResponse? cachedUser = await firstGetResponse.Content.ReadFromJsonAsync<UserResponse>();
        cachedUser!.FirstName.Should().Be(UserData.CacheInvalidationUserRequest.FirstName);

        // 3. Update the profile (triggers UserProfileUpdatedDomainEvent → Outbox → cache invalidation)
        var updateRequest = new UpdateUserProfileRequest(
            "CacheTest",
            "UpdatedLastName",
            "+34600000001",
            new DateOnly(1990, 3, 20),
            UserData.CacheInvalidationUserRequest.Password);

        HttpResponseMessage updateResponse = await HttpClient.PutAsJsonAsync("api/v1/users/profile", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Poll GET until cache is invalidated (FirstName changes from "CacheUser" to "CacheTest")
        var timeoutAt = DateTime.UtcNow.AddSeconds(15);
        UserResponse? freshUser = null;

        while (DateTime.UtcNow < timeoutAt)
        {
            HttpResponseMessage secondGetResponse = await HttpClient.GetAsync(getUserUri);
            secondGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            freshUser = await secondGetResponse.Content.ReadFromJsonAsync<UserResponse>();
            if (freshUser?.FirstName == "CacheTest")
            {
                break;
            }

            await Task.Delay(500);
        }

        // Assert
        freshUser.Should().NotBeNull();
        freshUser.FirstName.Should().Be("CacheTest");
        freshUser.LastName.Should().Be("UpdatedLastName");
    }
}
