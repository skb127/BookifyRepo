using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Apartments;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.IntegrationTests.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Apartments;

public class UpdateApartmentTests : BaseIntegrationTest
{
    public UpdateApartmentTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task UpdateApartment_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        var request = ApartmentData.ValidUpdateApartmentRequest;

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"api/v1/apartments/{Guid.CreateVersion7()}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateApartment_ShouldReturn403_WhenUserLacksAdminPermission()
    {
        // Arrange: login as a regular Registered user (no Admin role)
        string accessToken = await GetAccessToken(
            UserData.UpdateApartmentStandardUserRequest.Email,
            UserData.UpdateApartmentStandardUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        var request = ApartmentData.ValidUpdateApartmentRequest;

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"api/v1/apartments/{Guid.CreateVersion7()}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateApartment_ShouldReturn404_WhenApartmentDoesNotExist()
    {
        // Arrange: promote user to Admin
        string adminEmail = UserData.UpdateApartmentAdminUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string accessToken = await GetAccessToken(
            adminEmail,
            UserData.UpdateApartmentAdminUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        var request = ApartmentData.ValidUpdateApartmentRequest;

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"api/v1/apartments/{Guid.CreateVersion7()}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Apartment.NotFound");
    }

    [Fact]
    public async Task UpdateApartment_ShouldReturn400_WhenRequestIsInvalid()
    {
        // Arrange: promote user to Admin
        string adminEmail = UserData.UpdateApartmentAdminUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string accessToken = await GetAccessToken(
            adminEmail,
            UserData.UpdateApartmentAdminUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Fetch valid request and make it invalid
        var validRequest = ApartmentData.ValidUpdateApartmentRequest;
        var invalidRequest = new UpdateApartmentRequest(
            "", // Empty name fails validation
            validRequest.Description,
            validRequest.Address,
            validRequest.Price,
            validRequest.CleaningFee,
            validRequest.Amenities);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"api/v1/apartments/{Guid.CreateVersion7()}", invalidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateApartment_ShouldReturn204_AndPersistChanges_WhenRequestIsValid()
    {
        // Arrange: promote user to Admin
        string adminEmail = UserData.UpdateApartmentAdminUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string accessToken = await GetAccessToken(
            adminEmail,
            UserData.UpdateApartmentAdminUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // 1. Create an apartment first
        var createRequest = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", createRequest);
        createResponse.EnsureSuccessStatusCode();
        Uri locationUri = createResponse.Headers.Location!;

        // 2. Prepare update request
        var updateRequest = ApartmentData.ValidUpdateApartmentRequest;

        // Act: Update the created apartment
        HttpResponseMessage updateResponse = await HttpClient.PutAsJsonAsync(locationUri, updateRequest);

        // Assert Update Response
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Sub-request: Validate the GET endpoint returns the updated values
        HttpResponseMessage getResponse = await HttpClient.GetAsync(locationUri);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var apartmentResponse = await getResponse.Content.ReadFromJsonAsync<Application.Apartments.GetApartment.ApartmentResponse>();
        apartmentResponse.Should().NotBeNull();
        apartmentResponse.Name.Should().Be(updateRequest.Name);
        apartmentResponse.Description.Should().Be(updateRequest.Description);
        apartmentResponse.Price.Amount.Should().Be(updateRequest.Price.Amount);
        apartmentResponse.Price.Currency.Should().Be(updateRequest.Price.Currency);
        apartmentResponse.CleaningFee.Amount.Should().Be(updateRequest.CleaningFee.Amount);
        apartmentResponse.CleaningFee.Currency.Should().Be(updateRequest.CleaningFee.Currency);
        apartmentResponse.Address.City.Should().Be(updateRequest.Address.City);
    }
}
