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
#pragma warning disable IDE0047 // Remove unnecessary brackets
        var expectedTotalPrice = new Money((price.Amount * period.LengthInDays) + cleaningFee.Amount, price.Currency);
#pragma warning restore IDE0047 // Remove unnecessary brackets
        Apartment apartment = ApartmentData.Create(price, cleaningFee);
        var pricingService = new PricingService();

        // Act
        PricingDetails pricingDetails = pricingService.CalculatePrice(apartment, period);

        // Assert
        pricingDetails.TotalPrice.Should().Be(expectedTotalPrice);
    }
}
