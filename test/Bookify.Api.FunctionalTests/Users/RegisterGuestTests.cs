using System.Net;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users.Requests;
using Bookify.Api.FunctionalTests.Infrastructure;
using FluentAssertions;

namespace Bookify.Api.FunctionalTests.Users;

public class RegisterGuestTests : BaseFunctionalTest
{
    public RegisterGuestTests(FunctionalTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task RegisterGuest_ShouldReturnOk_WhenRequestIsValid()
    {
        // Arrange
        var request = new RegisterUserRequest("guest_func@test.com", "name", "lastname", "ClaveSegura123?",
            new DateOnly(2000, 1, 1));

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register/guest", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("", "name", "lastname", "123456")] // Empty email
    [InlineData("test.com", "name", "lastname", "123456")]
    [InlineData("@test.com", "name", "lastname", "123456")]
    [InlineData("test@", "name", "lastname", "123456")]
    [InlineData("test@test.com", "", "lastname", "123456")]
    [InlineData("test@test.com", "name", "", "123456")]
    [InlineData("test@test.com", "name", "lastname", "")]
    [InlineData("test@test.com", "name", "lastname", "1")]
    [InlineData("test@test.com", "name", "lastname", "ClaveSegura?")]
    [InlineData("test@test.com", "name", "lastname", "ClaveSegura1")]
    [InlineData("test@test.com", "name", "lastname", "clavesegura1?")]
    public async Task RegisterGuest_ShouldReturnBadRequest_WhenRequestIsInvalid(string email,
        string firstName,
        string lastName,
        string password)
    {
        var request = new RegisterUserRequest(email, firstName, lastName, password, new DateOnly(2000, 1, 1));

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register/guest", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
