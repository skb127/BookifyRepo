using System.Net;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Bookify.Application.IntegrationTests.Users;

public class RegisterUsersTests : BaseIntegrationTest
{
    public RegisterUsersTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    // [Fact(Skip = "This doesn't work at the moment")]
    public async Task Register_ShouldReturnOk_WhenRequestIsValid()
    {
        // Arrange
        var request = new RegisterUserRequest($"user-{Guid.NewGuid()}@test.com", "name", "lastname", "ClaveSegura1$", new DateOnly(2000, 1, 1));

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [ClassData(typeof(InvalidDataRegister))]
    public async Task Register_ShouldReturnBadRequest_WhenRequestIsInvalid(string email,
        string firstName,
        string lastName,
        string password)
    {
        var request = new RegisterUserRequest(email, firstName, lastName, password, new DateOnly(2000, 1, 1));

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
