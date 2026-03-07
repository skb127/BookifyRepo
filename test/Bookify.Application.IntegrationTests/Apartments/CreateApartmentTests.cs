using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Apartments;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.IntegrationTests.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Apartments;

[Collection("IntegrationTests")]
public class CreateApartmentTests : BaseIntegrationTest
{
    public CreateApartmentTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateApartment_ShouldReturnUnauthorized_WhenNoToken()
    {
        // Arrange
        var request = ApartmentData.ValidCreateApartmentRequest;

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/apartments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateApartment_ShouldReturnForbidden_WhenUserLacksAdminPermission()
    {
        // Arrange: login as a regular Registered user (no Admin role)
        string accessToken = await GetAccessToken(
            UserData.CreateApartmentStandardUserRequest.Email,
            UserData.CreateApartmentStandardUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        var request = ApartmentData.ValidCreateApartmentRequest;

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/apartments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [ClassData(typeof(InvalidDataCreateApartment))]
    public async Task CreateApartment_ShouldReturnBadRequest_WhenRequestIsInvalid(
        string name,
        string description,
        string country,
        string state,
        string zipCode,
        string city,
        string street,
        decimal priceAmount,
        string priceCurrency,
        decimal cleaningFeeAmount,
        string cleaningFeeCurrency,
        int[] amenities)
    {
        // Arrange: promote user to Admin
        string adminEmail = UserData.CreateApartmentAdminUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string accessToken = await GetAccessToken(
            adminEmail,
            UserData.CreateApartmentAdminUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        var request = new CreateApartmentRequest(
            name,
            description,
            new AddressRequest(country, state, zipCode, city, street),
            new MoneyRequest(priceAmount, priceCurrency),
            new MoneyRequest(cleaningFeeAmount, cleaningFeeCurrency),
            amenities);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/apartments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateApartment_ShouldReturnCreatedAtAction_WhenRequestIsValidAndUserIsAdmin()
    {
        // Arrange: promote user to Admin
        string adminEmail = UserData.CreateApartmentAdminUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string accessToken = await GetAccessToken(
            adminEmail,
            UserData.CreateApartmentAdminUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        var request = ApartmentData.ValidCreateApartmentRequest;

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/apartments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert Location Header is present
        response.Headers.Location.Should().NotBeNull();
        string location = response.Headers.Location!.ToString();
        location.Should().NotBeEmpty();

        // Sub-request: Validate the GET endpoint returns the newly created apartment
        HttpResponseMessage getResponse = await HttpClient.GetAsync(response.Headers.Location);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var apartmentResponse = await getResponse.Content.ReadFromJsonAsync<Application.Apartments.GetApartment.ApartmentResponse>();
        apartmentResponse.Should().NotBeNull();
        apartmentResponse.Name.Should().Be(request.Name);
        apartmentResponse.Description.Should().Be(request.Description);
        apartmentResponse.Price.Amount.Should().Be(request.Price.Amount);
        apartmentResponse.Price.Currency.Should().Be(request.Price.Currency);
    }
}
