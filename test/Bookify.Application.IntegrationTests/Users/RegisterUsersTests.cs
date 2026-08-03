using System.Net;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users.Requests;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Bookify.Application.IntegrationTests.Users;

public class RegisterUsersTests : BaseIntegrationTest
{
    public RegisterUsersTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Register_OldEndpoint_ShouldReturn410Gone()
    {
        // Arrange
        var request = new RegisterUserRequest($"user-{Guid.NewGuid()}@test.com", "name", "lastname", "ClaveSegura1$", new DateOnly(2000, 1, 1));

        // Act: POST api/v1/users/register (obsolete)
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }
}
