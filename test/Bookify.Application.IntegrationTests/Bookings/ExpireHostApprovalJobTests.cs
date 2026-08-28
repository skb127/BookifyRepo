using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Bookings.Requests;
using Bookify.Application.Bookings.GetBooking;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Bookings;

public class ExpireHostApprovalJobTests : BaseIntegrationTest
{
    public ExpireHostApprovalJobTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Execute_ShouldExpireBooking_WhenHostApprovalTtlExpires()
    {
        // Arrange
        var (apartmentId, guestToken) = await BookingTestHelpers.SetupApartmentAndGuestAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            guestToken);

        var reserveRequest = new ReserveBookingRequest(
            apartmentId,
            new DateOnly(2027, 5, 1),
            new DateOnly(2027, 5, 10))
        {
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2027, 5, 1),
            EndDate = new DateOnly(2027, 5, 10)
        };

        HttpResponseMessage reserveResponse = await HttpClient.PostAsJsonAsync("api/v1/bookings", reserveRequest);
        reserveResponse.EnsureSuccessStatusCode();

        Guid bookingId = await reserveResponse.Content.ReadFromJsonAsync<Guid>();

        // Act: Transition to Reserved by authorizing payment, which triggers the handler to cancel TTL1 and start TTL2
        DbContext.ChangeTracker.Clear();
        var booking = await DbContext.Set<Booking>().FindAsync(bookingId);
        booking.Should().NotBeNull();
        booking.AuthorizePayment("session_abc", "intent_abc");
        await DbContext.SaveChangesAsync();

        // Assert: Poll API for status transition to Expired due to 3s TTL2 configuration
        var timeoutAt = DateTime.UtcNow.AddSeconds(15);
        bool isExpired = false;

        while (DateTime.UtcNow < timeoutAt)
        {
            HttpResponseMessage response =
                await HttpClient.GetAsync(new Uri($"api/v1/bookings/{bookingId}", UriKind.Relative));
            if (response.IsSuccessStatusCode)
            {
                var bookingResponse = await response.Content.ReadFromJsonAsync<BookingResponse>();
                if (bookingResponse?.Status == (int)BookingStatus.Expired)
                {
                    isExpired = true;
                    break;
                }
            }

            await Task.Delay(500);
        }

        isExpired.Should()
            .BeTrue("the background job should have expired the booking when host approval TTL was reached");
    }
}
