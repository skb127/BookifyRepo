using System.Net;
using System.Net.Http.Headers;
using Bookify.Api.FunctionalTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Api.FunctionalTests.Users;

public class GetLoggedInUserTests : BaseFunctionalTest
{
    public GetLoggedInUserTests(FunctionalTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Get_ShouldReturnUnauthorized_WhenAccessTokenIsMissing()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/users/me", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_ShouldReturnUserInfo_WhenAccessTokenIsOK()
    {
        // Arrange
        string accessToken = await GetAccessToken(UserData.RegisterTestUserRequest3.Email, UserData.RegisterTestUserRequest3.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act
        HttpResponseMessage user = await HttpClient.GetAsync(new Uri("api/v1/users/me", UriKind.Relative));

        // Assert
        user.Should().NotBeNull();
        user.StatusCode.Should().Be(HttpStatusCode.OK);
    }

}
