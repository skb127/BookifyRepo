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

public class CancelBookingTests : BaseIntegrationTest
{
    private readonly MockEmailService _mockEmailService;

    public CancelBookingTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        _mockEmailService = factory.MockEmailService;
    }

    [Fact]
    public async Task CancelBooking_ShouldReturnFailure_WhenBookingIsNotFound()
    {
        // Arrange
        var bookingId = Guid.CreateVersion7();

        // 1. Create a dummy user just to be able to authenticate and call the cancellation endpoint
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
        HttpResponseMessage response = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelBooking_ShouldReturnFailure_WhenBookingIsInvalidForCancellation()
    {
        // Arrange
        var (_, _, bookingId, accessToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        // Update the booking status in the DB to Rejected so that cancellation fails
        await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE bookings
            SET status = {(int)BookingStatus.Rejected}
            WHERE id = {bookingId}
            """);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act - Attempt to cancel a rejected (invalid) booking
        HttpResponseMessage response = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Verify it was a NotConfirmed error
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(BookingErrors.NotConfirmed.Code);
    }

    [Fact]
    public async Task CancelBooking_ShouldSucceed_WhenBookingIsConfirmed()
    {
        // Arrange
        var (_, _, bookingId, accessToken, guestEmail) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Confirm the booking
        HttpResponseMessage confirmResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/confirmation", UriKind.Relative),
            null);
        confirmResponse.EnsureSuccessStatusCode();

        DateTime since = DateTime.UtcNow;

        // Act - Cancel the confirmed booking
        HttpResponseMessage cancelResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation", UriKind.Relative),
            null);

        // Assert Cancellation Success
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify Status is Cancelled
        HttpResponseMessage getResponse = await HttpClient.GetAsync(new Uri($"api/v1/bookings/{bookingId}", UriKind.Relative));
        getResponse.EnsureSuccessStatusCode();
        var bookingDetails = await getResponse.Content.ReadFromJsonAsync<BookingResponse>();

        bookingDetails.Should().NotBeNull();
        bookingDetails.Status.Should().Be((int)BookingStatus.Cancelled);

        // Verify Email was Sent (Outbox simulation via Mock)
        EmailMessage cancellationEmail = await _mockEmailService.WaitForEmailToAsync(guestEmail, "Booking Cancelled", since: since);

        _mockEmailService.HasEmailTo(guestEmail, since).Should().BeTrue("A cancellation email should be sent to the guest");
        cancellationEmail.Subject.Should().Be("Booking Cancelled");
    }

}
