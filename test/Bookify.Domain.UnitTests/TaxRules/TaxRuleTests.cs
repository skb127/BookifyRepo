using Bookify.Domain.Shared;
using Bookify.Domain.TaxRules;
using FluentAssertions;

namespace Bookify.Domain.UnitTests.TaxRules;

public class TaxRuleTests
{
    public static readonly TheoryData<TaxRate, Money, int, int, Money> CalculateTaxData = new()
    {
        // Percentage: 21% of 100 = 21
        { TaxRate.Percentage(0.21m), new Money(100.0m, Currency.Eur), 5, 2, new Money(21.0m, Currency.Eur) },
        // FixedPerNight: 5 per night for 3 nights = 15
        { TaxRate.PerNight(5.0m), new Money(200.0m, Currency.Eur), 3, 2, new Money(15.0m, Currency.Eur) },
        // FixedPerPersonPerNight: 2 per person per night for 4 nights, 3 guests = 24
        { TaxRate.PerPersonPerNight(2.0m), new Money(150.0m, Currency.Eur), 4, 3, new Money(24.0m, Currency.Eur) },
        // FixedPerBooking: 10 per booking = 10
        { TaxRate.PerBooking(10.0m), new Money(300.0m, Currency.Eur), 7, 4, new Money(10.0m, Currency.Eur) }
    };

    [Theory]
    [MemberData(nameof(CalculateTaxData))]
    public void CalculateTaxAmount_ShouldReturnExpectedAmount(
        TaxRate rate,
        Money totalPrice,
        int nights,
        int guests,
        Money expectedTax)
    {
        // Arrange
        var taxRule = TaxRule.Create(
            "ES",
            "Catalonia",
            "Barcelona",
            rate,
            "Tourist Tax",
            new DateOnly(2026, 1, 1),
            null,
            DateTime.UtcNow);

        // Act
        Money actualTax = taxRule.CalculateTaxAmount(totalPrice, nights, guests);

        // Assert
        actualTax.Should().Be(expectedTax);
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveToFalse_AndSetUpdatedOnUtc()
    {
        // Arrange
        var taxRule = TaxRule.Create(
            "ES",
            null,
            null,
            TaxRate.Percentage(0.10m),
            "VAT",
            new DateOnly(2026, 1, 1),
            null,
            DateTime.UtcNow);

        var utcNow = DateTime.UtcNow;

        // Act
        taxRule.Deactivate(utcNow);

        // Assert
        taxRule.IsActive.Should().BeFalse();
        taxRule.UpdatedOnUtc.Should().Be(utcNow);
    }
}
