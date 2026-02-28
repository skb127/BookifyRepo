using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Shared;
using Bookify.Domain.UnitTests.Apartments;
using FluentAssertions;

namespace Bookify.Domain.UnitTests.Bookings;

public class PricingServiceTests
{
    [Fact]
    public void CalculatePrice_ShouldReturnCorrectTotalPrice()
    {
        // Arrange
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        var expectedTotalPrice = new Money(price.Amount * period.LengthInDays, price.Currency);
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();

        // Act
        PricingDetails pricingDetails = pricingService.CalculatePrice(apartment, period);

        // Assert
        pricingDetails.TotalPrice.Should().Be(expectedTotalPrice);
    }

    [Fact]
    public void CalculatePrice_ShouldReturnCorrectTotalPrice_WhenCleaningFeeIsIncluded()
    {
        // Arrange
        var price = new Money(10.0m, Currency.Usd);
        var cleaningFee = new Money(99.99m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));

        var expectedTotalPrice = new Money(price.Amount * period.LengthInDays + cleaningFee.Amount, price.Currency);

        Apartment apartment = ApartmentData.Create(price, cleaningFee);
        var pricingService = new PricingService();

        // Act
        PricingDetails pricingDetails = pricingService.CalculatePrice(apartment, period);

        // Assert
        pricingDetails.TotalPrice.Should().Be(expectedTotalPrice);
    }

    [Fact]
    public void CalculatePrice_ShouldIncludeAmenitiesUpCharge_WhenApartmentHasAmenities()
    {
        // Arrange
        var price = new Money(100.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 10)); // 9 days = $900

        // GardenView (5%) + MountainView (5%) + AirConditioning (1%) + Parking (1%) = 12% total upcharge
        var amenities = new List<Amenity>
        {
            Amenity.GardenView,
            Amenity.MountainView,
            Amenity.AirConditioning,
            Amenity.Parking
        };

        Apartment apartment = ApartmentData.Create(price, null, amenities);
        var pricingService = new PricingService();

        // 900 base price
        var expectedBasePrice = new Money(900.0m, Currency.Usd);
        // 12% of 900 = 108
        var expectedUpCharge = new Money(108.0m, Currency.Usd);
        // Total = 1008
        var expectedTotalPrice = new Money(1008.0m, Currency.Usd);

        // Act
        PricingDetails pricingDetails = pricingService.CalculatePrice(apartment, period);

        // Assert
        pricingDetails.PriceForPeriod.Should().Be(expectedBasePrice);
        pricingDetails.AmenitiesUpCharge.Should().Be(expectedUpCharge);
        pricingDetails.TotalPrice.Should().Be(expectedTotalPrice);
    }

    [Fact]
    public void CalculatePrice_ShouldReturnZeroAmenitiesUpCharge_WhenNoAmenities()
    {
        // Arrange
        var price = new Money(100.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 10));

        Apartment apartment = ApartmentData.Create(price, null, new List<Amenity>());
        var pricingService = new PricingService();

        var expectedUpCharge = Money.Zero(Currency.Usd);

        // Act
        PricingDetails pricingDetails = pricingService.CalculatePrice(apartment, period);

        // Assert
        pricingDetails.AmenitiesUpCharge.Should().Be(expectedUpCharge);
    }
}
