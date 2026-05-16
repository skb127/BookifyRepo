using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Apartments;

public sealed record ApartmentAvailabilityResponse(bool IsAvailable, DateOnly StartDate, DateOnly EndDate);

public class CheckApartmentAvailabilityTests : BaseIntegrationTest
{
    public CheckApartmentAvailabilityTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CheckAvailability_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{Guid.CreateVersion7()}/availability?startDate=2028-06-01&endDate=2028-06-10", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CheckAvailability_ShouldReturn404_WhenApartmentDoesNotExist()
    {
        // Arrange: any authenticated user
        var (_, _, _, guestToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{Guid.CreateVersion7()}/availability?startDate=2028-06-01&endDate=2028-06-10", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CheckAvailability_ShouldReturnAvailableTrue_WhenNoOverlap()
    {
        // Arrange: apartment with an existing booking on 2027-01-01 → 2027-01-10
        var (_, apartmentId, _, guestToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act: query with dates entirely outside the booked range
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{apartmentId}/availability?startDate=2028-06-01&endDate=2028-06-10", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ApartmentAvailabilityResponse? body = await response.Content.ReadFromJsonAsync<ApartmentAvailabilityResponse>();
        body.Should().NotBeNull();
        body.IsAvailable.Should().BeTrue();
        body.StartDate.Should().Be(new DateOnly(2028, 6, 1));
        body.EndDate.Should().Be(new DateOnly(2028, 6, 10));
    }

    [Fact]
    public async Task CheckAvailability_ShouldReturnAvailableFalse_WhenBookingOverlaps()
    {
        // Arrange: apartment with an existing booking on 2027-01-01 → 2027-01-10
        var (_, apartmentId, _, guestToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act: query with dates that overlap the existing booking
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{apartmentId}/availability?startDate=2027-01-05&endDate=2027-01-08", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ApartmentAvailabilityResponse? body = await response.Content.ReadFromJsonAsync<ApartmentAvailabilityResponse>();
        body.Should().NotBeNull();
        body.IsAvailable.Should().BeFalse();
    }
}
