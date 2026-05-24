using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Bookings;
using Bookify.Application.Bookings.CompleteBookingsBatch;
using Bookify.Application.Bookings.NotifyCompletedBookings;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Application.IntegrationTests.Bookings;

public class NotifyCompletedBookingsTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory _factory;

    public NotifyCompletedBookingsTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Execute_ShouldNotifyAndMarkMultipleCompletedBookings()
    {
        // Isolation: Drain pre-existing state left by other tests.
        // The collection shares a single DB instance. Other tests may have created bookings that the
        // background CompleteBookingsJob auto-completed, leaving rows with
        // completed_notification_sent_at = null. We flush them here so that our
        // NotifyCompletedBookingsCommand(5) is guaranteed to pick up only our own bookings.
        await Sender.Send(new NotifyCompletedBookingsCommand(int.MaxValue));
        _factory.MockEmailService.Clear();

        // Arrange: Create 5 Bookings and Confirm them

        // Use the existing helper to create the first booking, which sets up the apartment and users
        var (_, apartmentId, bookingId1, guestToken, guestEmail) =
            await BookingTestHelpers.SetupReservedBookingAsync(this);

        // Create 4 more
        var bookingIds = new List<Guid> { bookingId1 };

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        for (int i = 2; i <= 5; i++)
        {
            var startDate = new DateOnly(2027, i, 1);
            var endDate = new DateOnly(2027, i, 10);

            var reserveRequest = new ReserveBookingRequest(apartmentId, startDate, endDate)
            {
                ApartmentId = apartmentId,
                StartDate = startDate,
                EndDate = endDate
            };

            var reserveResponse = await HttpClient.PostAsJsonAsync("api/v1/bookings", reserveRequest);
            reserveResponse.EnsureSuccessStatusCode();

            var newBookingId = await reserveResponse.Content.ReadFromJsonAsync<Guid>();
            bookingIds.Add(newBookingId);
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

        // Act: Execute CompleteBookings (Batch 1)
        var completeCommand = new CompleteBookingsBatchCommand();
        await Sender.Send(completeCommand);

        // Assert they are completed but NOT notified
        var completedBookings = await DbContext.Set<Booking>()
            .AsNoTracking()
            .Where(b => bookingIds.Contains(b.Id))
            .ToListAsync();

        completedBookings.Should().HaveCount(5);
        completedBookings.All(b => b.Status == BookingStatus.Completed).Should().BeTrue();
        completedBookings.All(b => b.CompletedNotificationSentAt == null).Should().BeTrue();
        _factory.MockEmailService.SentEmails.Where(e => e.Subject == "Stay Completed").Should().BeEmpty();

        // Act: Execute NotifyCompletedBookings (Batch 2)
        var notifyCommand = new NotifyCompletedBookingsCommand(5);
        await Sender.Send(notifyCommand);

        // Assert they are notified
        var notifiedBookings = await DbContext.Set<Booking>()
            .AsNoTracking()
            .Where(b => bookingIds.Contains(b.Id))
            .ToListAsync();

        notifiedBookings.All(b => b.CompletedNotificationSentAt != null).Should().BeTrue();

        // Assert 5 emails were sent with the correct subject
        var sentEmails = _factory.MockEmailService.SentEmails
            .Where(e => e.Subject == "Stay Completed")
            .ToList();

        sentEmails.Should().HaveCount(5);
        sentEmails.All(e => e.To == guestEmail).Should().BeTrue(); // All bookings belong to the same guest

        // Act: Execute NotifyCompletedBookings again (Idempotency check)
        _factory.MockEmailService.Clear();

        await Sender.Send(notifyCommand);

        // Assert no new emails were sent
        _factory.MockEmailService.SentEmails.Where(e => e.Subject == "Stay Completed").Should().BeEmpty();
    }
}
