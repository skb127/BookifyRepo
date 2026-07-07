using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Bookings;
using Bookify.Application.Bookings.GetBooking;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Bookings;

public class CloseStayBookingTests : BaseIntegrationTest
{
    public CloseStayBookingTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CloseStayBooking_ShouldReturn401_WhenNotAuthenticated()
    {
        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            new Uri($"api/v1/bookings/{Guid.NewGuid()}/close-stay", UriKind.Relative),
            new CloseStayRequest(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10)));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CloseStayBooking_ShouldReturn404_WhenBookingNotFound()
    {
        // Arrange
        var adminEmail = $"admin_{Guid.CreateVersion7()}@test.com";
        var password = "Password123!";
        var registerAdminCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));
        await Sender.Send(registerAdminCommand);
        await PromoteToAdminAsync(adminEmail);

        string adminToken = await GetAccessToken(adminEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            new Uri($"api/v1/bookings/{Guid.NewGuid()}/close-stay", UriKind.Relative),
            new CloseStayRequest(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10)));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CloseStayBooking_ShouldReturn204_WhenRequestIsValid()
    {
        // Arrange
        var password = "Password123!";
        var (_, bookingId, ownerToken, _, guestToken, _) =
            await BookingTestHelpers.SetupApartmentWithOwnerAsync(this, password);

        // Confirm the booking first
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            ownerToken);

        HttpResponseMessage confirmResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/confirmation", UriKind.Relative),
            null);
        confirmResponse.EnsureSuccessStatusCode();

        // Owner/Admin token is used to perform write operations (has BookingsWrite permission)
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            ownerToken);

        // Booking by default starts in 2027 (future). For CloseStay to work, the stay end date must be in the past.
        // Let's change utcNow? We cannot mock it here, but we can update the Booking's duration in database to be in the past
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dbBooking = await DbContext.Set<Booking>().FindAsync(bookingId);
        typeof(Booking).GetProperty(nameof(Booking.Duration))!.SetValue(dbBooking,
            DateRange.Create(today.AddDays(-10), today.AddDays(-2)));
        await DbContext.SaveChangesAsync();

        var checkInDate = today.AddDays(-10);
        var checkOutDate = today.AddDays(-2);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            new Uri($"api/v1/bookings/{bookingId}/close-stay", UriKind.Relative),
            new CloseStayRequest(checkInDate, checkOutDate));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify status in database is Completed
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            guestToken);

        HttpResponseMessage getResponse =
            await HttpClient.GetAsync(new Uri($"api/v1/bookings/{bookingId}", UriKind.Relative));
        getResponse.EnsureSuccessStatusCode();
        var bookingDetails = await getResponse.Content.ReadFromJsonAsync<BookingResponse>();
        bookingDetails.Should().NotBeNull();
        bookingDetails.Status.Should().Be((int)BookingStatus.Completed);
    }

    [Fact]
    public async Task CloseStayBooking_ShouldReturn400_WhenDatesAreMissing()
    {
        // Arrange
        var adminEmail = $"admin_{Guid.CreateVersion7()}@test.com";
        var password = "Password123!";
        var registerAdminCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));
        await Sender.Send(registerAdminCommand);
        await PromoteToAdminAsync(adminEmail);

        string adminToken = await GetAccessToken(adminEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminToken);

        // Act - Dates are missing (null)
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            new Uri($"api/v1/bookings/{Guid.NewGuid()}/close-stay", UriKind.Relative),
            new CloseStayRequest(null, null));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CloseStayBooking_ShouldReturn400_WhenCheckInDateIsAfterCheckOutDate()
    {
        // Arrange
        var adminEmail = $"admin_{Guid.CreateVersion7()}@test.com";
        var password = "Password123!";
        var registerAdminCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));
        await Sender.Send(registerAdminCommand);
        await PromoteToAdminAsync(adminEmail);

        string adminToken = await GetAccessToken(adminEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act - Check-in date (today) is after check-out date (yesterday)
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            new Uri($"api/v1/bookings/{Guid.NewGuid()}/close-stay", UriKind.Relative),
            new CloseStayRequest(today, today.AddDays(-1)));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
