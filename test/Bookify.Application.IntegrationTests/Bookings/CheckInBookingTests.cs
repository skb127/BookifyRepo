using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Bookings.GetBooking;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Application.IntegrationTests.Bookings;

public class CheckInBookingTests : BaseIntegrationTest
{
    private readonly MockEmailService _mockEmailService;

    public CheckInBookingTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        _mockEmailService = factory.MockEmailService;
    }

    [Fact]
    public async Task CheckInBooking_ShouldReturnFailure_WhenBookingIsNotFound()
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
        HttpResponseMessage response = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/check-in", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CheckInBooking_ShouldReturnFailure_WhenBookingIsNotConfirmed()
    {
        // Arrange
        var (_, _, bookingId, accessToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act - Attempt to check in a booking that is still Reserved (not Confirmed)
        HttpResponseMessage response = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/check-in", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(BookingErrors.NotConfirmed.Code);
    }

    [Fact]
    public async Task CheckInBooking_ShouldSucceed_WhenBookingIsConfirmed()
    {
        // Arrange
        var (_, _, bookingId, accessToken, guestEmail) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Update Duration in database so that it starts today and ends in 5 days
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dbBooking = await DbContext.Set<Booking>().FirstAsync(b => b.Id == bookingId);
        typeof(Booking).GetProperty(nameof(Booking.Duration))!.SetValue(dbBooking, DateRange.Create(today, today.AddDays(5)));
        await DbContext.SaveChangesAsync();

        // 1. Confirm the booking first
        HttpResponseMessage confirmResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/confirmation", UriKind.Relative),
            null);
        confirmResponse.EnsureSuccessStatusCode();

        DateTime since = DateTime.UtcNow;

        // 2. Act - Check In the confirmed booking
        HttpResponseMessage checkInResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/check-in", UriKind.Relative),
            null);

        // Assert Check-In Success
        checkInResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. Verify status in database
        HttpResponseMessage getResponse =
            await HttpClient.GetAsync(new Uri($"api/v1/bookings/{bookingId}", UriKind.Relative));
        getResponse.EnsureSuccessStatusCode();
        var bookingDetails = await getResponse.Content.ReadFromJsonAsync<BookingResponse>();

        bookingDetails.Should().NotBeNull();
        bookingDetails.Status.Should().Be((int)BookingStatus.InProgress);

        // 4. Verify CheckedIn email notification was sent
        EmailMessage checkInEmail =
            await _mockEmailService.WaitForEmailToAsync(guestEmail, "Booking Checked In", since: since);

        _mockEmailService.HasEmailTo(guestEmail, since).Should().BeTrue("A check-in email should be sent to the guest");
        checkInEmail.Subject.Should().Be("Booking Checked In");
    }

    [Fact]
    public async Task CheckInBooking_ShouldUpdateDurationStart_WhenGuestCheckInDateIsProvided()
    {
        // Arrange
        var (_, _, bookingId, accessToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Update Duration in database so that it starts yesterday and ends in 5 days,
        // and we set the new check-in date to yesterday.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var newCheckInDate = today.AddDays(-1);
        var dbBooking = await DbContext.Set<Booking>().FirstAsync(b => b.Id == bookingId);
        // Duration start must be at or before newCheckInDate
        typeof(Booking).GetProperty(nameof(Booking.Duration))!.SetValue(dbBooking,
            DateRange.Create(today.AddDays(-2), today.AddDays(5)));
        await DbContext.SaveChangesAsync();

        // 1. Confirm the booking first
        HttpResponseMessage confirmResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/confirmation", UriKind.Relative),
            null);
        confirmResponse.EnsureSuccessStatusCode();

        // 2. Act - Check In the confirmed booking with GuestCheckInDate
        var request = new Api.Controllers.Bookings.CheckInBookingRequest(newCheckInDate);
        HttpResponseMessage checkInResponse = await HttpClient.PutAsJsonAsync(
            new Uri($"api/v1/bookings/{bookingId}/check-in", UriKind.Relative),
            request);

        // Assert Check-In Success
        checkInResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. Verify status and duration start in database
        HttpResponseMessage getResponse =
            await HttpClient.GetAsync(new Uri($"api/v1/bookings/{bookingId}", UriKind.Relative));
        getResponse.EnsureSuccessStatusCode();
        var bookingDetails = await getResponse.Content.ReadFromJsonAsync<BookingResponse>();

        bookingDetails.Should().NotBeNull();
        bookingDetails.Status.Should().Be((int)BookingStatus.InProgress);
        bookingDetails.DurationStart.Should().Be(newCheckInDate);
    }
}
