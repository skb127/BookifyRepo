using System.Net;
using System.Net.Http.Headers;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Users;

[Collection("IntegrationTests")]
public class LogoutUserTests : BaseIntegrationTest
{
    public LogoutUserTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Logout_ShouldReturnUnauthorized_WhenUserTokenIsMissing()
    {
        // Act: Call logout endpoint without the Access Token to be unauthorized
        HttpResponseMessage response = await HttpClient.PostAsync(new Uri("api/v1/users/logout", UriKind.Relative), null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_ShouldReturnNoContent_WhenUserLogoutSucceeds()
    {
        // Arrange
        string accessToken = await GetAccessToken(UserData.LogoutTestUserRequest.Email, UserData.LogoutTestUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);
        
        // Act: Call logout endpoint with the Access Token
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);
        var logoutResponse = await HttpClient.PostAsync(new Uri("api/v1/users/logout", UriKind.Relative), null);
        
        // Assert
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

}
