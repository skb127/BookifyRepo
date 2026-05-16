using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.Users.GetLoggedInUser;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Users;

public class UpdateUserProfileTests : BaseIntegrationTest
{
    public UpdateUserProfileTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task UpdateProfile_ShouldReturnOk_WhenRequestIsValid()
    {
        // Arrange
        var user = UserData.UpdateProfileUserRequest;
        string accessToken = await GetAccessToken(user.Email, user.Password);

        var updatedFirstName = "UpdatedFirst";
        var updatedLastName = "UpdatedLast";
        var updatedPhone = "+34612345678";
        var updatedDateOfBirth = new DateOnly(1995, 6, 15);

        var request = new UpdateUserProfileRequest(
            updatedFirstName,
            updatedLastName,
            updatedPhone,
            updatedDateOfBirth,
            user.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync("api/v1/users/profile", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the profile was actually updated by fetching it
        HttpResponseMessage meResponse = await HttpClient.GetAsync(new Uri("api/v1/users/me", UriKind.Relative));
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        UserResponse? updatedUser = await meResponse.Content.ReadFromJsonAsync<UserResponse>();
        updatedUser.Should().NotBeNull();
        updatedUser.FirstName.Should().Be(updatedFirstName);
        updatedUser.LastName.Should().Be(updatedLastName);
        updatedUser.PhoneNumber.Should().Be(updatedPhone);
        updatedUser.DateOfBirth.Should().Be(updatedDateOfBirth);
    }

    [Fact]
    public async Task UpdateProfile_ShouldReturnBadRequest_WhenPasswordIsIncorrect()
    {
        // Arrange
        var user = UserData.UpdateProfileUserRequest2;
        string accessToken = await GetAccessToken(user.Email, user.Password);

        var request = new UpdateUserProfileRequest(
            "NewFirst",
            "NewLast",
            null,
            new DateOnly(1995, 6, 15),
            "WrongPassword123!");

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync("api/v1/users/profile", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [ClassData(typeof(InvalidDataUpdateProfile))]
    public async Task UpdateProfile_ShouldReturnBadRequest_WhenRequestIsInvalid(
        string firstName,
        string lastName,
        string? phoneNumber,
        string dateOfBirthString)
    {
        // Arrange
        var user = UserData.UpdateProfileUserRequest2;
        string accessToken = await GetAccessToken(user.Email, user.Password);

        var dateOfBirth = DateOnly.Parse(dateOfBirthString, CultureInfo.InvariantCulture);

        var request = new UpdateUserProfileRequest(
            firstName,
            lastName,
            phoneNumber,
            dateOfBirth,
            user.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync("api/v1/users/profile", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
