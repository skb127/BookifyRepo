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

public class MarkNoShowBookingTests : BaseIntegrationTest
{
    private readonly MockEmailService _mockEmailService;

    public MarkNoShowBookingTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        _mockEmailService = factory.MockEmailService;
    }

    [Fact]
    public async Task MarkNoShowBooking_ShouldReturnFailure_WhenBookingIsNotFound()
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
            new Uri($"api/v1/bookings/{bookingId}/no-show", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkNoShowBooking_ShouldReturnFailure_WhenBookingIsNotConfirmed()
    {
        // Arrange
        var (_, _, bookingId, accessToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act - Attempt to mark a booking as no-show that is still Reserved (not Confirmed)
        HttpResponseMessage response = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/no-show", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(BookingErrors.NotConfirmed.Code);
    }

    [Fact]
    public async Task MarkNoShowBooking_ShouldReturnFailure_WhenCurrentDateIsBeforeOrOnStartDate()
    {
        // Arrange
        var (_, _, bookingId, accessToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // 1. Confirm the booking first (but keeping its start date in 2027, i.e., in the future relative to now)
        HttpResponseMessage confirmResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/confirmation", UriKind.Relative),
            null);
        confirmResponse.EnsureSuccessStatusCode();

        // 2. Act - Try to mark as no-show (should fail because today is before 2027)
        HttpResponseMessage response = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/no-show", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(BookingErrors.CannotMarkNoShow.Code);
    }

    [Fact]
    public async Task MarkNoShowBooking_ShouldSucceed_WhenBookingIsConfirmed_AndStartDateIsInPast()
    {
        // Arrange
        var (_, _, bookingId, accessToken, guestEmail) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // 1. Confirm the booking first
        HttpResponseMessage confirmResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/confirmation", UriKind.Relative),
            null);
        confirmResponse.EnsureSuccessStatusCode();

        // 2. Force start date to be in the past to satisfy no-show date validation
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE bookings
            SET duration_start = {yesterday}
            WHERE id = {bookingId}
            """);

        DateTime since = DateTime.UtcNow;

        // 3. Act - Mark as no-show
        HttpResponseMessage response = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/no-show", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. Verify status in database
        HttpResponseMessage getResponse = await HttpClient.GetAsync(new Uri($"api/v1/bookings/{bookingId}", UriKind.Relative));
        getResponse.EnsureSuccessStatusCode();
        var bookingDetails = await getResponse.Content.ReadFromJsonAsync<BookingResponse>();

        bookingDetails.Should().NotBeNull();
        bookingDetails.Status.Should().Be((int)BookingStatus.NoShow);

        // 5. Verify NoShow email notification was sent
        EmailMessage noShowEmail = await _mockEmailService.WaitForEmailToAsync(guestEmail, "Booking Marked as No-Show", since: since);

        _mockEmailService.HasEmailTo(guestEmail, since).Should().BeTrue("A no-show email should be sent to the guest");
        noShowEmail.Subject.Should().Be("Booking Marked as No-Show");
    }
}
