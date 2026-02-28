using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Bookings;
using Bookify.Application.Bookings.GetBooking;
using Bookify.Application.IntegrationTests.Apartments;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Bookings;

[Collection("IntegrationTests")]
public class ReserveBookingTests : BaseIntegrationTest
{
    public ReserveBookingTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn201_WhenRequestIsValid()
    {
        // Arrange
        // 1. Create an admin to create the apartment
        var adminEmail = $"admin_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerAdminCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));
        await Sender.Send(registerAdminCommand);
        await PromoteToAdminAsync(adminEmail);

        // 2. Create apartment mapping to Admin context
        string adminToken = await GetAccessToken(adminEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, adminToken);
        var aptData = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        // 3. Create a guest to reserve
        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        var guestUserIdResponse = await Sender.Send(registerGuestCommand);
        var guestUserId = guestUserIdResponse.Value;

        string guestToken = await GetAccessToken(guestEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var request = new ReserveBookingRequest(
            apartmentId,
            new DateOnly(2028, 1, 1),
            new DateOnly(2028, 1, 10))
        {
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2028, 1, 1),
            EndDate = new DateOnly(2028, 1, 10)
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().StartWith($"https://localhost/api/v1/bookings/");

        var bookingId = await response.Content.ReadFromJsonAsync<Guid>();
        bookingId.Should().NotBeEmpty();

        // Verify the booking was created in DB for the GUEST
        HttpResponseMessage getResponse = await HttpClient.GetAsync(response.Headers.Location);
        getResponse.EnsureSuccessStatusCode();
        var bookingResponse = await getResponse.Content.ReadFromJsonAsync<BookingResponse>();
        bookingResponse.Should().NotBeNull();
        bookingResponse.UserId.Should().Be(guestUserId);
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        var request = new ReserveBookingRequest(
            Guid.NewGuid(),
            new DateOnly(2028, 1, 1),
            new DateOnly(2028, 1, 10))
        {
            ApartmentId = Guid.NewGuid(),
            StartDate = new DateOnly(2028, 1, 1),
            EndDate = new DateOnly(2028, 1, 10)
        };

        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
