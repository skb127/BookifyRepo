using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.Bookings.GetUserBookings;
using Bookify.Application.Common;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Bookings;

[Collection("IntegrationTests")]
public class GetUserBookingsTests : BaseIntegrationTest
{
    public GetUserBookingsTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetMyBookings_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/bookings", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyBookings_ShouldReturn400_WhenPageSizeExceeds100()
    {
        // Arrange
        var (_, _, _, guestToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/bookings?pageSize=101", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be("Validation error");
    }

    [Fact]
    public async Task GetMyBookings_ShouldReturn400_WhenPageIsZero()
    {
        // Arrange
        var (_, _, _, guestToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/bookings?page=0", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be("Validation error");
    }

    [Fact]
    public async Task GetMyBookings_ShouldReturnEmptyList_WhenUserHasNoBookings()
    {
        // Arrange
        var guestEmail = $"guest_empty_{Guid.CreateVersion7()}@test.com";
        var password = "Password123!";
        var registerGuestCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));

        await Sender.Send(registerGuestCommand);
        string guestToken = await GetAccessToken(guestEmail, password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/bookings", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResponse = await response.Content.ReadFromJsonAsync<PagedResponse<UserBookingResponse>>();
        pagedResponse.Should().NotBeNull();
        pagedResponse.TotalCount.Should().Be(0);
        pagedResponse.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyBookings_ShouldReturnBookings_WhenUserHasBookings()
    {
        // Arrange
        var (_, _, bookingId, guestToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/bookings", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResponse = await response.Content.ReadFromJsonAsync<PagedResponse<UserBookingResponse>>();
        pagedResponse.Should().NotBeNull();
        pagedResponse.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        pagedResponse.Items.Should().Contain(b => b.Id == bookingId);
        pagedResponse.Items.Should().AllSatisfy(b => b.Status.Should().Be((int)BookingStatus.Reserved));
    }

    [Fact]
    public async Task GetMyBookings_ShouldFilterByStatus_WhenStatusIsProvided_AndNoMatch()
    {
        // Arrange
        var (_, _, _, guestToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act - Request Confirmed (2), but we only have Reserved (1)
        int confirmedStatus = (int)BookingStatus.Confirmed;
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/bookings?status={confirmedStatus}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResponse = await response.Content.ReadFromJsonAsync<PagedResponse<UserBookingResponse>>();
        pagedResponse.Should().NotBeNull();
        pagedResponse.TotalCount.Should().Be(0);
        pagedResponse.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyBookings_ShouldReturnMultipleBookings_WhenUserHasBookingsInDifferentStates()
    {
        // Arrange
        var (_, _, booking1Reserved, booking2Confirmed, guestToken, _) = await BookingTestHelpers.SetupMultipleBookingsAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act - Get all
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/bookings", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResponse = await response.Content.ReadFromJsonAsync<PagedResponse<UserBookingResponse>>();
        pagedResponse.Should().NotBeNull();
        pagedResponse.TotalCount.Should().BeGreaterThanOrEqualTo(2);

        // Ensure both bookings are returned
        pagedResponse.Items.Should().Contain(b => b.Id == booking1Reserved && b.Status == (int)BookingStatus.Reserved);
        pagedResponse.Items.Should().Contain(b => b.Id == booking2Confirmed && b.Status == (int)BookingStatus.Confirmed);
    }

    [Fact]
    public async Task GetMyBookings_ShouldFilterByStatus_WhenStatusMatchesSomeBookings()
    {
        // Arrange
        var (_, _, booking1Reserved, booking2Confirmed, guestToken, _) = await BookingTestHelpers.SetupMultipleBookingsAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act - Filter by Confirmed (2)
        int confirmedStatus = (int)BookingStatus.Confirmed;
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/bookings?status={confirmedStatus}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResponse = await response.Content.ReadFromJsonAsync<PagedResponse<UserBookingResponse>>();
        pagedResponse.Should().NotBeNull();

        // Ensure only the confirmed booking is returned
        pagedResponse.Items.Should().Contain(b => b.Id == booking2Confirmed);
        pagedResponse.Items.Should().NotContain(b => b.Id == booking1Reserved);
        pagedResponse.Items.Should().AllSatisfy(b => b.Status.Should().Be(confirmedStatus));
    }
}
