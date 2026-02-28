using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bookify.Application.IntegrationTests.Users;

[Collection("IntegrationTests")]
public class RefreshTokenUserTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory _factory;

    public RefreshTokenUserTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Refresh_ShouldReturnOk_AndNewAccessToken_AndSetRefreshTokenCookie_WhenFlowIsCorrect()
    {
        // Arrange: Login to get the accessToken and refreshToken
        string accessToken = await GetAccessToken(UserData.RefreshTokenUserRequest.Email, UserData.RefreshTokenUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act: call refresh endpoint with the same HttpClient (sends cookie automatically)
        using HttpResponseMessage refreshResponse = await HttpClient.PostAsync(
            new Uri("api/v1/users/refresh", UriKind.Relative),
            null);

        // Assert 1: status code OK
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert 2: body contains a non-empty accessToken
        var accessTokenResponse = await refreshResponse.Content.ReadFromJsonAsync<AccessTokenOnlyResponse>();

        accessTokenResponse.Should().BeOfType<AccessTokenOnlyResponse>();
        accessTokenResponse.AccessToken.Should().NotBeNullOrWhiteSpace();

        // Assert 3: a refreshToken cookie has been set
        refreshResponse.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? setCookies)
            .Should().BeTrue();

        setCookies!.Should().Contain(c => c.Contains("refreshToken="));
    }

    [Fact]
    public async Task Refresh_ShouldReturnUnauthorized_WhenRefreshTokenCookieIsMissing()
    {
        // Arrange
        // Create a new client that doesn't handle cookies for this test
        using var clientWithoutCookies = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost")
        });

        // Act
        using var response = await clientWithoutCookies.PostAsync(
            new Uri("api/v1/users/refresh", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
