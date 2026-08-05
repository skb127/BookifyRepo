using System.Net;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users.Requests;
using Bookify.Api.FunctionalTests.Infrastructure;
using FluentAssertions;

namespace Bookify.Api.FunctionalTests.Users;

#pragma warning disable CA1515
public class RegisterHostTests : BaseFunctionalTest
#pragma warning restore CA1515
{
    public RegisterHostTests(FunctionalTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task RegisterHost_ShouldReturnOk_WhenRequestIsValid()
    {
        // Arrange
        var request = new RegisterHostRequest("host_func@test.com", "Host", "User", "ClaveSegura123?", new DateOnly(1990, 1, 1), "+34612345678");

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register/host", request);

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
    public async Task RegisterHost_ShouldReturnBadRequest_WhenRequestIsInvalid(string email,
        string firstName,
        string lastName,
        string password)
    {
        var request = new RegisterHostRequest(email, firstName, lastName, password, new DateOnly(1990, 1, 1), "+34612345678");

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register/host", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
