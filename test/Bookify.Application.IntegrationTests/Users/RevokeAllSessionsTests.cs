using System.Net;
using System.Net.Http.Headers;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Users;

[Collection("IntegrationTests")]
public class RevokeAllSessionsTests : BaseIntegrationTest
{
    public RevokeAllSessionsTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task RevokeAllSessions_ShouldReturnUnauthorized_WhenNoToken()
    {
        // Act
        HttpResponseMessage response = await HttpClient.PostAsync(
            new Uri("api/v1/users/revoke-all-sessions", UriKind.Relative), null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokeAllSessions_ShouldReturnNoContent_WhenSuccessful()
    {
        // Arrange
        string accessToken = await GetAccessToken(
            UserData.RevokeSessionsUserRequest.Email,
            UserData.RevokeSessionsUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync(
            new Uri("api/v1/users/revoke-all-sessions", UriKind.Relative), null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
