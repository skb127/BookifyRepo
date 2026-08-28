using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Bookings.Requests;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Application.IntegrationTests.Bookings;

public class ReserveBookingIdempotencyTests : BaseIntegrationTest
{
    public ReserveBookingIdempotencyTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn201_OnFirstRequest_WithIdempotenceKey()
    {
        // Arrange
        var (apartmentId, guestToken) = await BookingTestHelpers.SetupApartmentAndGuestAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var idempotenceKey = Guid.NewGuid().ToString();
        HttpClient.DefaultRequestHeaders.Add("Idempotence-Key", idempotenceKey);

        var request = new ReserveBookingRequest(apartmentId, new DateOnly(2029, 6, 1), new DateOnly(2029, 6, 10))
        {
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2029, 6, 1),
            EndDate = new DateOnly(2029, 6, 10)
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var bookingId = await response.Content.ReadFromJsonAsync<Guid>();
        bookingId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ReserveBooking_ShouldNotCreateDuplicate_WhenSameIdempotenceKeyUsedThreeTimes()
    {
        // Arrange
        var (apartmentId, guestToken) = await BookingTestHelpers.SetupApartmentAndGuestAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var idempotenceKey = Guid.NewGuid().ToString();
        HttpClient.DefaultRequestHeaders.Add("Idempotence-Key", idempotenceKey);

        var request = new ReserveBookingRequest(apartmentId, new DateOnly(2029, 6, 11), new DateOnly(2029, 6, 20))
        {
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2029, 6, 11),
            EndDate = new DateOnly(2029, 6, 20)
        };

        // Act — three identical requests with the same idempotency key
        HttpResponseMessage first = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);
        HttpResponseMessage second = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);
        HttpResponseMessage third = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);

        // Assert — all three return 201
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        third.StatusCode.Should().Be(HttpStatusCode.Created);

        var firstId = await first.Content.ReadFromJsonAsync<Guid>();
        var secondId = await second.Content.ReadFromJsonAsync<Guid>();
        var thirdId = await third.Content.ReadFromJsonAsync<Guid>();

        // All three return the same booking ID (cached response)
        secondId.Should().Be(firstId);
        thirdId.Should().Be(firstId);

        // Exactly one booking exists in DB
        int count = await DbContext.Set<Booking>()
            .CountAsync(b => b.Id == firstId).ConfigureAwait(true);
        count.Should().Be(1);
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn422_WhenSameKeyReusedWithDifferentApartment()
    {
        // Arrange
        var (apartmentId1, guestToken) = await BookingTestHelpers.SetupApartmentAndGuestAsync(this);
        var (apartmentId2, _) = await BookingTestHelpers.SetupApartmentAndGuestAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var idempotenceKey = Guid.NewGuid().ToString();
        HttpClient.DefaultRequestHeaders.Add("Idempotence-Key", idempotenceKey);

        var originalRequest =
            new ReserveBookingRequest(apartmentId1, new DateOnly(2029, 7, 1), new DateOnly(2029, 7, 10))
            {
                ApartmentId = apartmentId1,
                StartDate = new DateOnly(2029, 7, 1),
                EndDate = new DateOnly(2029, 7, 10)
            };

        var differentRequest =
            new ReserveBookingRequest(apartmentId2, new DateOnly(2029, 7, 1), new DateOnly(2029, 7, 10))
            {
                ApartmentId = apartmentId2,
                StartDate = new DateOnly(2029, 7, 1),
                EndDate = new DateOnly(2029, 7, 10)
            };

        // Act — first request succeeds, second reuses key with different body
        HttpResponseMessage first = await HttpClient.PostAsJsonAsync("api/v1/bookings", originalRequest);
        HttpResponseMessage second = await HttpClient.PostAsJsonAsync("api/v1/bookings", differentRequest);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity); // 422 — body hash mismatch
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn201_WithoutIdempotenceKey()
    {
        // Arrange
        var (apartmentId, guestToken) = await BookingTestHelpers.SetupApartmentAndGuestAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var request = new ReserveBookingRequest(apartmentId, new DateOnly(2029, 8, 1), new DateOnly(2029, 8, 10))
        {
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2029, 8, 1),
            EndDate = new DateOnly(2029, 8, 10)
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn201_WithInvalidGuidAsIdempotenceKey()
    {
        // Arrange
        var (apartmentId, guestToken) = await BookingTestHelpers.SetupApartmentAndGuestAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        HttpClient.DefaultRequestHeaders.Add("Idempotence-Key", "not-a-guid");

        var request = new ReserveBookingRequest(apartmentId, new DateOnly(2029, 9, 1), new DateOnly(2029, 9, 10))
        {
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2029, 9, 1),
            EndDate = new DateOnly(2029, 9, 10)
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
