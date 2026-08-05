using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.Bookings.GetBookings;
using Bookify.Application.Common;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Bookings;

public class GetBookingsTests : BaseIntegrationTest
{
    public GetBookingsTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetBookings_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/bookings", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBookings_ShouldReturn403_WhenUserIsNotAdmin()
    {
        // Arrange
        var guestEmail = $"guest_forbidden_{Guid.CreateVersion7()}@test.com";
        var password = "Password123!";
        var registerGuestCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));

        await Sender.Send(registerGuestCommand);
        string guestToken = await GetAccessToken(guestEmail, password);

        // Guest only has "Registered" role, lacks "bookings:read"
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/bookings", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetBookings_ShouldReturn400_WhenPageSizeExceeds100()
    {
        // Arrange
        var adminEmail = await CreateAuthAdminAsync();
        string adminToken = await GetAccessToken(adminEmail, "Password123!");
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, adminToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/bookings?pageSize=101", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be("Validation error");
    }

    [Fact]
    public async Task GetBookings_ShouldReturn200_WhenAdminCallsEndpoint()
    {
        // Arrange
        // Admin user is created internally inside the helper to create the apartment
        var (_, _, bookingId, _, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        // We need an admin token to call the admin endpoint. Create a new admin just for this request.
        var adminEmail = await CreateAuthAdminAsync();
        string adminToken = await GetAccessToken(adminEmail, "Password123!");
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, adminToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/bookings", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResponse = await response.Content.ReadFromJsonAsync<PagedResponse<BookingSummaryResponse>>();
        pagedResponse.Should().NotBeNull();
        pagedResponse.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        pagedResponse.Items.Should().Contain(b => b.Id == bookingId);
    }

    [Fact]
    public async Task GetBookings_ShouldFilterByUserId_WhenUserIdIsProvided()
    {
        // Arrange
        var (userId, _, bookingId, _, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        var adminEmail = await CreateAuthAdminAsync();
        string adminToken = await GetAccessToken(adminEmail, "Password123!");
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, adminToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/bookings?userId={userId}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResponse = await response.Content.ReadFromJsonAsync<PagedResponse<BookingSummaryResponse>>();
        pagedResponse.Should().NotBeNull();
        pagedResponse.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        pagedResponse.Items.Should().AllSatisfy(b => b.UserId.Should().Be(userId));
        pagedResponse.Items.Should().Contain(b => b.Id == bookingId);
    }

    [Fact]
    public async Task GetBookings_ShouldFilterByStatus_WhenStatusIsProvided_AndNoMatch()
    {
        // Arrange
        await BookingTestHelpers.SetupReservedBookingAsync(this);

        var adminEmail = await CreateAuthAdminAsync();
        string adminToken = await GetAccessToken(adminEmail, "Password123!");
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, adminToken);

        // Act - Request Completed (5), but we only created a Reserved (1)
        int completedStatus = (int)BookingStatus.Completed;
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/bookings?status={completedStatus}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResponse = await response.Content.ReadFromJsonAsync<PagedResponse<BookingSummaryResponse>>();
        pagedResponse.Should().NotBeNull();

        // Ensure no completed bookings match (or at least not the one we just made)
        // Note: other tests might leave data, but looking for an empty list is the safest isolated assumption if DB is clean
        pagedResponse.Items.Should().NotContain(b => b.Status != completedStatus);
    }

    [Fact]
    public async Task GetBookings_ShouldReturnMultipleBookings_WhenUserHasBookingsInDifferentStates()
    {
        // Arrange
        var (userId, _, booking1Reserved, booking2Confirmed, _, _) = await BookingTestHelpers.SetupMultipleBookingsAsync(this);

        var adminEmail = await CreateAuthAdminAsync();
        string adminToken = await GetAccessToken(adminEmail, "Password123!");
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, adminToken);

        // Act - Filter by userId to isolate the data created in this test
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/bookings?userId={userId}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResponse = await response.Content.ReadFromJsonAsync<PagedResponse<BookingSummaryResponse>>();
        pagedResponse.Should().NotBeNull();
        pagedResponse.TotalCount.Should().Be(2);

        // Ensure both bookings are returned
        pagedResponse.Items.Should().Contain(b => b.Id == booking1Reserved && b.Status == (int)BookingStatus.Reserved);
        pagedResponse.Items.Should().Contain(b => b.Id == booking2Confirmed && b.Status == (int)BookingStatus.Confirmed);
    }

    [Fact]
    public async Task GetBookings_ShouldFilterByStatus_WhenStatusMatchesSomeBookings()
    {
        // Arrange
        var (userId, _, booking1Reserved, booking2Confirmed, _, _) = await BookingTestHelpers.SetupMultipleBookingsAsync(this);

        var adminEmail = await CreateAuthAdminAsync();
        string adminToken = await GetAccessToken(adminEmail, "Password123!");
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, adminToken);

        // Act - Filter by User ID + Status = Reserved
        int reservedStatus = (int)BookingStatus.Reserved;
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/bookings?userId={userId}&status={reservedStatus}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResponse = await response.Content.ReadFromJsonAsync<PagedResponse<BookingSummaryResponse>>();
        pagedResponse.Should().NotBeNull();

        // Ensure only the reserved booking is returned
        pagedResponse.Items.Should().Contain(b => b.Id == booking1Reserved);
        pagedResponse.Items.Should().NotContain(b => b.Id == booking2Confirmed);
        pagedResponse.Items.Should().AllSatisfy(b => b.Status.Should().Be(reservedStatus));
    }

    private async Task<string> CreateAuthAdminAsync()
    {
        var adminEmail = $"admin_caller_{Guid.CreateVersion7()}@test.com";
        var password = "Password123!";
        var registerAdminCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));

        await Sender.Send(registerAdminCommand).ConfigureAwait(false);
        await PromoteToAdminAsync(adminEmail).ConfigureAwait(false);
        return adminEmail;
    }
}
