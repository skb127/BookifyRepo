using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.Bookings.GetPriceEstimate;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Apartments;

[Collection("IntegrationTests")]
public class GetApartmentPriceEstimateTests : BaseIntegrationTest
{
    public GetApartmentPriceEstimateTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetPriceEstimate_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        var apartmentId = Guid.NewGuid();
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 5);
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{apartmentId}/price-estimate?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPriceEstimate_ShouldReturn400_WhenEndDateIsBeforeStartDate()
    {
        // Arrange
        var (apartmentId, _, _, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var startDate = new DateOnly(2025, 1, 5);
        var endDate = new DateOnly(2025, 1, 1); // Invalid: ends before starts

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{apartmentId}/price-estimate?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPriceEstimate_ShouldReturn404_WhenApartmentNotFound()
    {
        // Arrange
        var (_, _, _, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var apartmentId = Guid.CreateVersion7(); // Random ID
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 5);

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{apartmentId}/price-estimate?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPriceEstimate_ShouldReturn200_WithCorrectBreakdown()
    {
        // Arrange
        // Setups an apartment using ValidCreateApartmentRequest
        var (apartmentId, _, _, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 6); // 5 days

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{apartmentId}/price-estimate?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var estimate = await response.Content.ReadFromJsonAsync<PriceEstimateResponse>();

        estimate.Should().NotBeNull();

        // 5 days * 100 USD = 500 USD
        estimate.PriceForPeriodAmount.Should().Be(500.0m);
        estimate.PriceForPeriodCurrency.Should().Be("USD");

        // Fixed 50 USD cleaning fee
        estimate.CleaningFeeAmount.Should().Be(50.0m);
        estimate.CleaningFeeCurrency.Should().Be("USD");

        // No amenities = 0 upcharge
        estimate.AmenitiesUpChargeAmount.Should().Be(0m);
        estimate.AmenitiesUpChargeCurrency.Should().Be("USD");

        // Total: 500 + 50 + 0 = 550
        estimate.TotalAmount.Should().Be(550.0m);
        estimate.TotalCurrency.Should().Be("USD");

        estimate.LengthInDays.Should().Be(5);
    }
}
