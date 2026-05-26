using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Bookings;
using Bookify.Application.Bookings.GetBooking;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace Bookify.Application.IntegrationTests.Bookings;

public class CheckOutBookingTests : BaseIntegrationTest
{
    public CheckOutBookingTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CheckOutBooking_ShouldReturnFailure_WhenBookingIsNotFound()
    {
        // Arrange
        var bookingId = Guid.CreateVersion7();
        var guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";
        var password = "Password123!";

        var registerGuestCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        _ = await Sender.Send(registerGuestCommand);

        string accessToken = await GetAccessToken(guestEmail, password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            new Uri($"api/v1/bookings/{bookingId}/check-out", UriKind.Relative),
            new BookingReasonRequest(null, null));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CheckOutBooking_ShouldReturnFailure_WhenBookingIsNotInProgress()
    {
        // Arrange
        var (_, _, bookingId, accessToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act - Attempt to check out a booking that is still Reserved (not InProgress)
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            new Uri($"api/v1/bookings/{bookingId}/check-out", UriKind.Relative),
            new BookingReasonRequest(null, null));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(BookingErrors.NotInProgress.Code);
    }

    [Fact]
    public async Task CheckOutBooking_ShouldReturnBadRequest_WhenRequestBodyIsMissing()
    {
        // Arrange
        var (_, _, bookingId, accessToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act - Attempt to check out without providing the body
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync<BookingReasonRequest?>(
            new Uri($"api/v1/bookings/{bookingId}/check-out", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CheckOutBooking_ShouldSucceed_WhenBookingIsInProgress_WithReason()
    {
        // Arrange
        var (_, _, bookingId, accessToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // 1. Confirm the booking first
        HttpResponseMessage confirmResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/confirmation", UriKind.Relative),
            null);
        confirmResponse.EnsureSuccessStatusCode();

        // 2. Check in the booking to transition it to InProgress
        HttpResponseMessage checkInResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/check-in", UriKind.Relative),
            null);
        checkInResponse.EnsureSuccessStatusCode();

        // 3. Act - Check Out the booking with an early departure reason
        var reasonRequest = new BookingReasonRequest(ReasonType.EarlyDeparture, "Family emergency");
        HttpResponseMessage checkOutResponse = await HttpClient.PutAsJsonAsync(
            new Uri($"api/v1/bookings/{bookingId}/check-out", UriKind.Relative),
            reasonRequest);

        // Assert Check-Out Success
        checkOutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. Verify status in database
        HttpResponseMessage getResponse = await HttpClient.GetAsync(new Uri($"api/v1/bookings/{bookingId}", UriKind.Relative));
        getResponse.EnsureSuccessStatusCode();
        var bookingDetails = await getResponse.Content.ReadFromJsonAsync<BookingResponse>();

        bookingDetails.Should().NotBeNull();
        bookingDetails.Status.Should().Be((int)BookingStatus.Completed);
    }
}
