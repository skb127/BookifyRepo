using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.IntegrationTests.Users;
using Bookify.Application.Users.RegisterHost;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Apartments;

public class DeleteApartmentTests : BaseIntegrationTest
{
    public DeleteApartmentTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task DeleteApartment_ShouldReturn401_WhenNotAuthenticated()
    {
        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync(new Uri($"api/v1/apartments/{Guid.CreateVersion7()}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteApartment_ShouldReturn403_WhenUserLacksAdminPermission()
    {
        // Arrange: login as a regular Registered user (no Host role)
        string accessToken = await GetAccessToken(
            UserData.DeleteApartmentStandardUserRequest.Email,
            UserData.DeleteApartmentStandardUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync(new Uri($"api/v1/apartments/{Guid.CreateVersion7()}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteApartment_ShouldReturn404_WhenApartmentDoesNotExist()
    {
        // Arrange: register a Host user
        string hostEmail = $"host_del404_{Guid.NewGuid()}@test.com";
        string password = "Password123!";
        _ = await Sender.Send(new RegisterHostCommand(hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678"));

        string accessToken = await GetAccessToken(hostEmail, password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync(new Uri($"api/v1/apartments/{Guid.CreateVersion7()}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Apartment.NotFound");
    }

    [Fact]
    public async Task DeleteApartment_ShouldReturn400_WhenApartmentHasActiveBookings()
    {
        // Arrange: use helper to create host + guest + apartment + Reserved booking
        var (_, apartmentId, _, _, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        string hostEmail = $"host_delactive_{Guid.NewGuid()}@test.com";
        string password = "Password123!";
        _ = await Sender.Send(new RegisterHostCommand(hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678"));

        string hostToken = await GetAccessToken(hostEmail, password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            hostToken);

        // Act: try to delete the apartment that has a reserved booking
        HttpResponseMessage response = await HttpClient.DeleteAsync(new Uri($"api/v1/apartments/{apartmentId}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Apartment.HasActiveBookings");
    }

    [Fact]
    public async Task DeleteApartment_ShouldReturn204_WhenApartmentExists()
    {
        // Arrange: register a Host user
        string hostEmail = $"host_del204_{Guid.NewGuid()}@test.com";
        string password = "Password123!";
        _ = await Sender.Send(new RegisterHostCommand(hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678"));

        string accessToken = await GetAccessToken(hostEmail, password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // 1. Create an apartment first
        var createRequest = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", createRequest);
        createResponse.EnsureSuccessStatusCode();
        Uri locationUri = createResponse.Headers.Location!;

        // Act: Delete the created apartment
        HttpResponseMessage deleteResponse = await HttpClient.DeleteAsync(locationUri);

        // Assert Delete Response
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Sub-request: Validate the GET endpoint returns 404 since it's soft-deleted
        HttpResponseMessage getResponse = await HttpClient.GetAsync(locationUri);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
