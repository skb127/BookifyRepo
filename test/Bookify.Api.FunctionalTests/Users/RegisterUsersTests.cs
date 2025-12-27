using System.Net;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users;
using Bookify.Api.FunctionalTests.Infrastructure;
using Bookify.Domain.Users;
using FluentAssertions;

namespace Bookify.Api.FunctionalTests.Users;

#pragma warning disable CA1515
public class RegisterUsersTests : BaseFunctionalTest
#pragma warning restore CA1515
{
    public RegisterUsersTests(FunctionalTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Register_ShouldReturnOk_WhenRequestIsValid()
    {
        // Arrange
        var request = new RegisterUserRequest("user@test.com", "name", "lastname", "123456");

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register", request);

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
    [InlineData("test@test.com", "name", "lastname", "12")] 
    [InlineData("test@test.com", "name", "lastname", "123")] 
    [InlineData("test@test.com", "name", "lastname", "1234")] 
    public async Task Register_ShouldReturnBadRequest_WhenRequestIsInvalid(string email,
        string firstName,
        string lastName,
        string password)
    {
        var request = new RegisterUserRequest(email, firstName, lastName, password);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
