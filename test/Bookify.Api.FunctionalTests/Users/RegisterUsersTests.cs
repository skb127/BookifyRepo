using System.Net;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users.Requests;
using Bookify.Api.FunctionalTests.Infrastructure;
using FluentAssertions;

namespace Bookify.Api.FunctionalTests.Users;

public class RegisterUsersTests : BaseFunctionalTest
{
    public RegisterUsersTests(FunctionalTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Register_OldEndpoint_ShouldReturn410Gone()
    {
        // Arrange
        var request = new RegisterUserRequest("user@test.com", "name", "lastname", "ClaveSegura123?",
            new DateOnly(2000, 1, 1));

        // Act: POST api/v1/users/register (obsolete)
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }
}
