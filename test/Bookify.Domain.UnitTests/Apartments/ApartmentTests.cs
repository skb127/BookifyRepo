using Bookify.Domain.Apartments;
using Bookify.Domain.Shared;
using Bookify.Domain.UnitTests.Infrastructure;
using FluentAssertions;

namespace Bookify.Domain.UnitTests.Apartments;

public class ApartmentTests : BaseTest
{
    [Fact]
    public void Create_ShouldSetPropertyValues_WhenValidParametersAreProvided()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var name = new Name("Test Apartment");
        var description = new Description("Test Description");
        var address = new Address("Country", "State", "ZipCode", "City", "Street");
        var price = new Money(100.0m, Currency.Usd);
        var cleaningFee = new Money(50.0m, Currency.Usd);
        var amenities = new List<Amenity> { Amenity.WiFi, Amenity.Parking };

        // Act
        var apartment = Apartment.Create(ownerId, name, description, address, price, cleaningFee, amenities);

        // Assert
        apartment.Should().NotBeNull();
        apartment.Id.Should().NotBeEmpty();
        apartment.OwnerId.Should().Be(ownerId);
        apartment.Name.Should().Be(name);
        apartment.Description.Should().Be(description);
        apartment.Address.Should().Be(address);
        apartment.Price.Should().Be(price);
        apartment.CleaningFee.Should().Be(cleaningFee);
        apartment.Amenities.Should().BeEquivalentTo(amenities);
    }
}
