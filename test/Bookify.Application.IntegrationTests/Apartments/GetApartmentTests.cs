using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.IntegrationTests.Users;
using Bookify.Application.Users.RegisterHost;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Apartments;

public class GetApartmentTests : BaseIntegrationTest
{
    public GetApartmentTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetApartment_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{Guid.CreateVersion7()}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetApartment_ShouldReturn404_WhenApartmentDoesNotExist()
    {
        // Arrange: Use standard user to get an access token
        string accessToken = await GetAccessToken(
            UserData.CreateApartmentStandardUserRequest.Email,
            UserData.CreateApartmentStandardUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{Guid.CreateVersion7()}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Let's verify it contains the expected error code.
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Apartment.NotFound");
    }

    [Fact]
    public async Task GetApartment_ShouldReturn200_WhenApartmentExists()
    {
        // Arrange: register a Host user and create apartment
        string hostEmail = $"host_getapt_{Guid.NewGuid()}@test.com";
        string password = "Password123!";
        _ = await Sender.Send(new RegisterHostCommand(hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678"));

        string accessToken = await GetAccessToken(hostEmail, password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        var createRequest = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", createRequest);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();
        Uri locationUri = createResponse.Headers.Location!;

        // Act: retrieve the created apartment
        // We'll reuse the current token
        HttpResponseMessage getResponse = await HttpClient.GetAsync(locationUri);

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var apartmentResponse = await getResponse.Content.ReadFromJsonAsync<Application.Apartments.GetApartment.ApartmentResponse>();

        apartmentResponse.Should().NotBeNull();
        apartmentResponse.Name.Should().Be(createRequest.Name);
        apartmentResponse.Description.Should().Be(createRequest.Description);

        apartmentResponse.Price.Should().NotBeNull();
        apartmentResponse.Price.Amount.Should().Be(createRequest.Price.Amount);
        apartmentResponse.Price.Currency.Should().Be(createRequest.Price.Currency);

        apartmentResponse.CleaningFee.Should().NotBeNull();
        apartmentResponse.CleaningFee.Amount.Should().Be(createRequest.CleaningFee.Amount);
        apartmentResponse.CleaningFee.Currency.Should().Be(createRequest.CleaningFee.Currency);

        apartmentResponse.Address.Should().NotBeNull();
        apartmentResponse.Address.Country.Should().Be(createRequest.Address.Country);
        apartmentResponse.Address.State.Should().Be(createRequest.Address.State);
        apartmentResponse.Address.ZipCode.Should().Be(createRequest.Address.ZipCode);
        apartmentResponse.Address.City.Should().Be(createRequest.Address.City);
        apartmentResponse.Address.Street.Should().Be(createRequest.Address.Street);

        apartmentResponse.Amenities.Should().BeEmpty(); // ValidCreateApartmentRequest has [] amenities

        // OwnerId should be present and valid
        apartmentResponse.OwnerId.Should().NotBeEmpty();
    }
}
