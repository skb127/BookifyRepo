using System.Net;
using System.Net.Http.Json;
using Bookify.Application.Bookings.GetPriceEstimate;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Bookify.Application.IntegrationTests.Bookings;

public class GetPriceEstimateTests : BaseIntegrationTest
{
    public GetPriceEstimateTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetPriceEstimate_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        var apartmentId = Guid.NewGuid();
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 5);

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/price-estimate?apartmentId={apartmentId}&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPriceEstimate_ShouldReturn400_WhenApartmentIdIsEmpty()
    {
        // Arrange
        var (_, _, _, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", guestToken);

        var apartmentId = Guid.Empty;
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 5);

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/price-estimate?apartmentId={apartmentId}&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPriceEstimate_ShouldReturn400_WhenEndDateIsBeforeStartDate()
    {
        // Arrange
        var (apartmentId, _, _, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", guestToken);

        var startDate = new DateOnly(2025, 1, 5);
        var endDate = new DateOnly(2025, 1, 1); // Invalid: ends before starts

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/price-estimate?apartmentId={apartmentId}&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPriceEstimate_ShouldReturn404_WhenApartmentNotFound()
    {
        // Arrange
        var (_, _, _, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", guestToken);

        var apartmentId = Guid.NewGuid(); // Random ID
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 5);

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/price-estimate?apartmentId={apartmentId}&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPriceEstimate_ShouldReturn200_WithCorrectBreakdown_WhenApartmentHasNoAmenities()
    {
        // Arrange
        // Setups an apartment using ValidCreateApartmentRequest (Price: 100 USD, Cleaning: 50 USD, Amenities: [])
        var (apartmentId, _, _, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", guestToken);

        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 6); // 5 days

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/price-estimate?apartmentId={apartmentId}&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}", UriKind.Relative));

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

    [Fact]
    public async Task GetPriceEstimate_ShouldReturn200_WithAmenitiesUpcharge_WhenApartmentHasAmenities()
    {
        // Arrange
        var ownerEmail = $"owner_{Guid.NewGuid()}@test.com";
        _ = await Sender.Send(new Bookify.Application.Users.RegisterUser.RegisterUserCommand(ownerEmail, "Owner", "Admin", "Password123!", new DateOnly(1990, 1, 1)));
        await PromoteToAdminAsync(ownerEmail);
        var ownerToken = await GetAccessToken(ownerEmail, "Password123!");

        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerToken);

        var createResponse = await HttpClient.PostAsJsonAsync(
            "api/v1/apartments",
            Apartments.ApartmentData.ValidCreateApartmentWithAmenitiesRequest);

        createResponse.EnsureSuccessStatusCode();
        var apartmentId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        _ = await Sender.Send(new Bookify.Application.Users.RegisterUser.RegisterUserCommand(guestEmail, "Guest", "User", "Password123!", new DateOnly(1995, 5, 5)));
        var guestToken = await GetAccessToken(guestEmail, "Password123!");

        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", guestToken);

        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 6); // 5 days

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/price-estimate?apartmentId={apartmentId}&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var estimate = await response.Content.ReadFromJsonAsync<PriceEstimateResponse>();

        estimate.Should().NotBeNull();

        // Base Price: 5 days * 100 USD = 500 USD
        estimate.PriceForPeriodAmount.Should().Be(500.0m);

        // Upcharge: 6% of 500 = 30 USD
        estimate.AmenitiesUpChargeAmount.Should().Be(30.0m);

        // Cleaning: 50 USD
        estimate.CleaningFeeAmount.Should().Be(50.0m);

        // Total: 500 + 30 + 50 = 580
        estimate.TotalAmount.Should().Be(580.0m);
    }
}
