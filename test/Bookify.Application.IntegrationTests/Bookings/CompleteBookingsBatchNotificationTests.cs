using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Bookings;
using Bookify.Application.Bookings.CompleteBookingsBatch;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Application.IntegrationTests.Bookings;

public class CompleteBookingsBatchNotificationTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory _factory;

    public CompleteBookingsBatchNotificationTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Execute_ShouldCompleteBookingsAndAutomaticallySendEmailsViaOutbox()
    {
        // Arrange
        _factory.MockEmailService.Clear();

        // Use the existing helper to create the first booking, which sets up the apartment and users
        var (_, _, bookingId1, guestToken, guestEmail) =
            await BookingTestHelpers.SetupReservedBookingAsync(this);

        // Create 4 more bookings
        var bookingIds = new List<Guid> { bookingId1 };

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Retrieve the real apartment ID via the booking info API endpoint
        var bookingResponse =
            await HttpClient.GetFromJsonAsync<Bookify.Application.Bookings.GetBooking.BookingResponse>(
                $"api/v1/bookings/{bookingId1}");
        bookingResponse.Should().NotBeNull();
        var realApartmentId = bookingResponse.ApartmentId;

        for (int i = 2; i <= 5; i++)
        {
            var startDate = new DateOnly(2027, i, 1);
            var endDate = new DateOnly(2027, i, 10);

            var reserveRequest = new ReserveBookingRequest(realApartmentId, startDate, endDate)
            {
                ApartmentId = realApartmentId,
                StartDate = startDate,
                EndDate = endDate
            };

            var reserveResponse = await HttpClient.PostAsJsonAsync("api/v1/bookings", reserveRequest);
            reserveResponse.EnsureSuccessStatusCode();

            var newBookingId = await reserveResponse.Content.ReadFromJsonAsync<Guid>();
            bookingIds.Add(newBookingId);

            // Transition the booking from PendingPayment to Reserved
            Booking? booking = await DbContext.Set<Booking>().FindAsync(newBookingId);
            booking!.AuthorizePayment($"session_{newBookingId}", $"intent_{newBookingId}");
            await DbContext.SaveChangesAsync();
        }

        // Confirm all 5 bookings bypassing HTTP to speed up (we already have tests for confirm booking)
        foreach (var id in bookingIds)
        {
            var confirmCommand = new Bookify.Application.Bookings.ConfirmBooking.ConfirmBookingCommand(id);
            var confirmResult = await Sender.Send(confirmCommand);
            confirmResult.IsSuccess.Should().BeTrue();
        }

        // Move all 5 bookings to the past so they are eligible for completion
        // Also check in all 5 bookings so they are set to InProgress
        foreach (var id in bookingIds)
        {
            var checkInCommand = new Bookify.Application.Bookings.CheckInBooking.CheckInBookingCommand(id);
            await Sender.Send(checkInCommand);

            await DbContext.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE bookings
                SET duration_end = '2000-01-01'
                WHERE id = {id}");
        }

        // Clear mock emails before the actual test actions begin
        _factory.MockEmailService.Clear();

        // Act: Execute CompleteBookings
        var completeCommand = new CompleteBookingsBatchCommand();
        var completeResult = await Sender.Send(completeCommand);
        completeResult.IsSuccess.Should().BeTrue();

        // Assert they are completed in the DB
        var completedBookings = await DbContext.Set<Booking>()
            .AsNoTracking()
            .Where(b => bookingIds.Contains(b.Id))
            .ToListAsync();

        completedBookings.Should().HaveCount(5);
        completedBookings.All(b => b.Status == BookingStatus.Completed).Should().BeTrue();

        // Poll the mock email service to verify that the outbox processor automatically picked up the events
        // and dispatched the customized completed booking emails.
        var emailTimeoutAt = DateTime.UtcNow.AddSeconds(5);
        bool allEmailsSent = false;

        while (DateTime.UtcNow < emailTimeoutAt)
        {
            var sentEmails = _factory.MockEmailService.SentEmails;
            int matchingCount = sentEmails.Count(e =>
                e.Subject.StartsWith("Stay Completed - ", StringComparison.Ordinal) &&
                e.To == guestEmail);

            if (matchingCount == 5)
            {
                allEmailsSent = true;
                break;
            }

            await Task.Delay(200);
        }

        allEmailsSent.Should()
            .BeTrue(
                "The outbox processor should have automatically dispatched all 5 Stay Completed emails with their respective booking IDs in the subject.");

        // Assert specific booking IDs are in the subject lines
        var deliveredEmails = _factory.MockEmailService.SentEmails
            .Where(e => e.Subject.StartsWith("Stay Completed - ", StringComparison.Ordinal))
            .ToList();

        foreach (var id in bookingIds)
        {
            deliveredEmails.Any(e => e.Subject == $"Stay Completed - {id}").Should().BeTrue();
        }
    }
}
