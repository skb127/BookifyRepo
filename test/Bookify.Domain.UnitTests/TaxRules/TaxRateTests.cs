using Bookify.Domain.TaxRules;
using FluentAssertions;

namespace Bookify.Domain.UnitTests.TaxRules;

public class TaxRateTests
{
    [Fact]
    public void FactoryMethods_ShouldSetCorrectTaxType()
    {
        // Act
        var percentage = TaxRate.Percentage(10.0m);
        var perNight = TaxRate.PerNight(5.0m);
        var perPersonPerNight = TaxRate.PerPersonPerNight(2.0m);
        var perBooking = TaxRate.PerBooking(15.0m);

        // Assert
        percentage.Type.Should().Be(TaxType.Percentage);
        percentage.Value.Should().Be(10.0m);

        perNight.Type.Should().Be(TaxType.FixedPerNight);
        perNight.Value.Should().Be(5.0m);

        perPersonPerNight.Type.Should().Be(TaxType.FixedPerPersonPerNight);
        perPersonPerNight.Value.Should().Be(2.0m);

        perBooking.Type.Should().Be(TaxType.FixedPerBooking);
        perBooking.Value.Should().Be(15.0m);
    }

    [Fact]
    public void Create_ShouldSetGivenValueAndType()
    {
        // Act
        var rate = TaxRate.Create(12.5m, TaxType.Percentage);

        // Assert
        rate.Value.Should().Be(12.5m);
        rate.Type.Should().Be(TaxType.Percentage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10.5)]
    public void IsValid_ShouldReturnTrue_WhenValueIsZeroOrPositive(decimal value)
    {
        // Arrange
        var rate = TaxRate.Percentage(value);

        // Act
        var isValid = rate.IsValid();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_ShouldReturnFalse_WhenValueIsNegative()
    {
        // Arrange
        var rate = TaxRate.Percentage(-1.0m);

        // Act
        var isValid = rate.IsValid();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void TaxRates_ShouldBeEqualByValue_WhenPropertiesAreEqual()
    {
        // Arrange
        var rate1 = TaxRate.Percentage(10.0m);
        var rate2 = TaxRate.Percentage(10.0m);

        // Act & Assert
        rate1.Should().Be(rate2);
    }
}
