using Bookify.Domain.Apartments;
using Bookify.Domain.Apartments.Events;
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
        var apartment = Apartment.Create(ownerId, name, description, address, price, cleaningFee, amenities, DateTime.UtcNow);

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
        apartment.InstantBooking.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldSetInstantBookingTrue_WhenInstantBookingIsTrue()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var name = new Name("Test Apartment");
        var description = new Description("Test Description");
        var address = new Address("Country", "State", "ZipCode", "City", "Street");
        var price = new Money(100.0m, Currency.Usd);
        var cleaningFee = new Money(50.0m, Currency.Usd);
        var amenities = new List<Amenity> { Amenity.WiFi };

        // Act
        var apartment = Apartment.Create(ownerId, name, description, address, price, cleaningFee, amenities, DateTime.UtcNow, instantBooking: true);

        // Assert
        apartment.InstantBooking.Should().BeTrue();
    }

    [Fact]
    public void Update_ShouldMutateAllProperties_WhenValidParametersAreProvided()
    {
        // Arrange
        var apartment = ApartmentData.Create(new Money(100.0m, Currency.Usd));

        var newName = new Name("Updated Apartment");
        var newDescription = new Description("Updated Description");
        var newAddress = new Address("Portugal", "Lisbon", "1000-001", "Lisbon", "Rua Nova 5");
        var newPrice = new Money(200.0m, Currency.Eur);
        var newCleaningFee = new Money(40.0m, Currency.Eur);
        var newAmenities = new List<Amenity> { Amenity.Gym, Amenity.Spa };

        // Act
        apartment.Update(newName, newDescription, newAddress, newPrice, newCleaningFee, newAmenities, DateTime.UtcNow, false, null, 2, 5);

        // Assert
        apartment.Name.Should().Be(newName);
        apartment.Description.Should().Be(newDescription);
        apartment.Address.Should().Be(newAddress);
        apartment.Price.Should().Be(newPrice);
        apartment.CleaningFee.Should().Be(newCleaningFee);
        apartment.Amenities.Should().BeEquivalentTo(newAmenities);

        var domainEvent = AssertDomainEventWasPublished<ApartmentUpdatedDomainEvent>(apartment);
        domainEvent.ApartmentId.Should().Be(apartment.Id);
    }

    [Fact]
    public void Update_ShouldMutateInstantBooking_WhenDifferentValueIsProvided()
    {
        // Arrange
        var apartment = ApartmentData.Create(new Money(100.0m, Currency.Usd), instantBooking: false);

        var newName = new Name("Updated Apartment");
        var newDescription = new Description("Updated Description");
        var newAddress = new Address("Portugal", "Lisbon", "1000-001", "Lisbon", "Rua Nova 5");
        var newPrice = new Money(200.0m, Currency.Eur);
        var newCleaningFee = new Money(40.0m, Currency.Eur);
        var newAmenities = new List<Amenity> { Amenity.Gym };

        // Act
        apartment.Update(newName, newDescription, newAddress, newPrice, newCleaningFee, newAmenities, DateTime.UtcNow, true, null, 2, 5);

        // Assert
        apartment.InstantBooking.Should().BeTrue();
    }

    [Fact]
    public void Update_ShouldClearAmenities_WhenEmptyListProvided()
    {
        // Arrange
        var apartment = ApartmentData.Create(
            new Money(100.0m, Currency.Usd),
            amenities: new List<Amenity> { Amenity.WiFi, Amenity.Parking });

        // Act
        apartment.Update(
            new Name("Name"),
            new Description("Description"),
            new Address("Country", "State", "ZipCode", "City", "Street"),
            new Money(100.0m, Currency.Usd),
            Money.Zero(),
            [],
            DateTime.UtcNow,
            false,
            null,
            1,
            3);

        // Assert
        apartment.Amenities.Should().BeEmpty();
    }

    [Fact]
    public void Delete_ShouldSetDeletedAt_WhenCalled()
    {
        // Arrange
        var apartment = ApartmentData.Create(new Money(100.0m, Currency.Usd));
        DateTime before = DateTime.UtcNow;

        // Act
        apartment.Delete(DateTime.UtcNow);

        // Assert
        apartment.DeletedAt.Should().NotBeNull();
        apartment.DeletedAt.Should().BeOnOrAfter(before);
        apartment.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        var domainEvent = AssertDomainEventWasPublished<ApartmentDeletedDomainEvent>(apartment);
        domainEvent.ApartmentId.Should().Be(apartment.Id);
    }

    [Fact]
    public void Delete_ShouldNotChangeName_WhenCalled()
    {
        // Arrange
        var apartment = ApartmentData.Create(new Money(100.0m, Currency.Usd));
        Name originalName = apartment.Name;
        Money originalPrice = apartment.Price;

        // Act
        apartment.Delete(DateTime.UtcNow);

        // Assert
        apartment.Name.Should().Be(originalName);
        apartment.Price.Should().Be(originalPrice);
    }
}
