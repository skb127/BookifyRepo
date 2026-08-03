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
using Microsoft.EntityFrameworkCore;

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

        var registerGuestCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        _ = await Sender.Send(registerGuestCommand);

        string accessToken = await GetAccessToken(guestEmail, password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            new Uri($"api/v1/bookings/{bookingId}/check-out", UriKind.Relative),
            new CheckOutBookingRequest());

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
            new CheckOutBookingRequest());

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
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync<CheckOutBookingRequest?>(
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

        // Update Duration in database so that it starts yesterday and ends in 5 days
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dbBooking = await DbContext.Set<Booking>().FirstAsync(b => b.Id == bookingId);
        typeof(Booking).GetProperty(nameof(Booking.Duration))!.SetValue(dbBooking,
            DateRange.Create(today.AddDays(-1), today.AddDays(5)));
        await DbContext.SaveChangesAsync();

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
        var reasonRequest = new CheckOutBookingRequest(ReasonType.EarlyDeparture, "Family emergency");
        HttpResponseMessage checkOutResponse = await HttpClient.PutAsJsonAsync(
            new Uri($"api/v1/bookings/{bookingId}/check-out", UriKind.Relative),
            reasonRequest);

        // Assert Check-Out Success
        checkOutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. Verify status in database
        HttpResponseMessage getResponse =
            await HttpClient.GetAsync(new Uri($"api/v1/bookings/{bookingId}", UriKind.Relative));
        getResponse.EnsureSuccessStatusCode();
        var bookingDetails = await getResponse.Content.ReadFromJsonAsync<BookingResponse>();

        bookingDetails.Should().NotBeNull();
        bookingDetails.Status.Should().Be((int)BookingStatus.Completed);
    }

    [Fact]
    public async Task CheckOutBooking_ShouldUpdateDurationEnd_WhenGuestCheckOutDateIsProvided()
    {
        // Arrange
        var (_, _, bookingId, accessToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Update Duration in database so that it starts 3 days ago and ends in 5 days,
        // and we set the new check-out date to yesterday.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var newCheckOutDate = today.AddDays(-1);
        var dbBooking = await DbContext.Set<Booking>().FirstAsync(b => b.Id == bookingId);
        // Duration start must be at or before newCheckOutDate
        typeof(Booking).GetProperty(nameof(Booking.Duration))!.SetValue(dbBooking,
            DateRange.Create(today.AddDays(-3), today.AddDays(5)));
        await DbContext.SaveChangesAsync();

        // 1. Confirm the booking first
        HttpResponseMessage confirmResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/confirmation", UriKind.Relative),
            null);
        confirmResponse.EnsureSuccessStatusCode();

        // 2. Act - Check In the booking to transition it to InProgress
        HttpResponseMessage checkInResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/check-in", UriKind.Relative),
            null);
        checkInResponse.EnsureSuccessStatusCode();

        // 3. Act - Check Out the booking with GuestCheckOutDate
        var request = new CheckOutBookingRequest(null, null, newCheckOutDate);
        HttpResponseMessage checkOutResponse = await HttpClient.PutAsJsonAsync(
            new Uri($"api/v1/bookings/{bookingId}/check-out", UriKind.Relative),
            request);

        // Assert Check-Out Success
        checkOutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. Verify status and duration end in database
        HttpResponseMessage getResponse =
            await HttpClient.GetAsync(new Uri($"api/v1/bookings/{bookingId}", UriKind.Relative));
        getResponse.EnsureSuccessStatusCode();
        var bookingDetails = await getResponse.Content.ReadFromJsonAsync<BookingResponse>();

        bookingDetails.Should().NotBeNull();
        bookingDetails.Status.Should().Be((int)BookingStatus.Completed);
        bookingDetails.DurationEnd.Should().Be(newCheckOutDate);
    }
}
