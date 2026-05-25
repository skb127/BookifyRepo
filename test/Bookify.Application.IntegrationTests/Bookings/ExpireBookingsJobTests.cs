using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Application.IntegrationTests.Bookings;

public class ExpireBookingsJobTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory _factory;

    public ExpireBookingsJobTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Execute_ShouldExpireBookingsAndSendEmail_WhenExpiresAtIsPast()
    {
        // Arrange
        _factory.MockEmailService.Clear();

        var (_, _, bookingId, guestToken, _) =
            await BookingTestHelpers.SetupReservedBookingAsync(this);

        // Update expires_at to a past date directly in the database
        await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE bookings
            SET expires_at = '2000-01-01'
            WHERE id = {bookingId}
            """);

        // Act & Assert
        // We'll poll the database until the status changes to Expired, or timeout after 5 seconds
        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme, guestToken);

        var timeoutAt = DateTime.UtcNow.AddSeconds(5);
        bool isExpired = false;

        while (DateTime.UtcNow < timeoutAt)
        {
            var response = await HttpClient.GetAsync(new Uri($"api/v1/bookings/{bookingId}", UriKind.Relative));

            if (response.IsSuccessStatusCode)
            {
                var booking = await response.Content.ReadFromJsonAsync<Bookify.Application.Bookings.GetBooking.BookingResponse>();

                if (booking?.Status == (int)BookingStatus.Expired)
                {
                    isExpired = true;
                    break;
                }
            }

            await Task.Delay(500); // Check every 500ms
        }

        isExpired.Should().BeTrue("The background job should have expired the booking within the timeout period.");

        // Wait a small moment for outbox processor to run and dispatch the email
        var emailTimeoutAt = DateTime.UtcNow.AddSeconds(3);
        bool emailSent = false;

        while (DateTime.UtcNow < emailTimeoutAt)
        {
            var sentEmails = _factory.MockEmailService.SentEmails;
            if (sentEmails.Any(e => e.Subject == $"Booking Expired - {bookingId}"))
            {
                emailSent = true;
                break;
            }

            await Task.Delay(200);
        }

        emailSent.Should().BeTrue("The outbox processor should have processed the BookingExpiredDomainEvent and sent the email.");
    }
}
