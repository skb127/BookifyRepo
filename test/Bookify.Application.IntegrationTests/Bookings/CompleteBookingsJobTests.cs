using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Application.IntegrationTests.Bookings;

public class CompleteBookingsJobTests : BaseIntegrationTest
{
    public CompleteBookingsJobTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    // turbo
    public async Task Execute_ShouldCompleteBookings_WhenDurationEndIsPast()
    {
        // Arrange
        var (_, bookingId, _, _, guestToken, _) =
            await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);

        // Confirm it via command so we bypass HTTP layer for speed, or we can use the endpoint. 
        // Using command directly:
        var confirmCommand = new Bookify.Application.Bookings.ConfirmBooking.ConfirmBookingCommand(bookingId);
        await Sender.Send(confirmCommand);

        // NOTE: Future phases will require explicit CheckIn before completing. Ensure status is set to InProgress here as part of that flow.
        await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE bookings
            SET duration_end = '2000-01-01',
                status = {(int)BookingStatus.InProgress}
            WHERE id = {bookingId}
            """);

        // Act & Assert
        // The job is configured to run every second in the test factory
        // We'll poll the database until the status changes to Completed, or timeout after 5 seconds
        // Need to add Authentication header to query the endpoint
        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme, guestToken);

        var timeoutAt = DateTime.UtcNow.AddSeconds(5);
        bool isCompleted = false;

        while (DateTime.UtcNow < timeoutAt)
        {
            var response = await HttpClient.GetAsync(new Uri($"api/v1/bookings/{bookingId}", UriKind.Relative));

            if (response.IsSuccessStatusCode)
            {
                var booking = await response.Content.ReadFromJsonAsync<Bookify.Application.Bookings.GetBooking.BookingResponse>();

                if (booking?.Status == (int)BookingStatus.Completed)
                {
                    isCompleted = true;
                    break;
                }
            }

            await Task.Delay(500); // Check every 500ms
        }

        isCompleted.Should().BeTrue("The background job should have completed the booking within the timeout period.");
    }
}
