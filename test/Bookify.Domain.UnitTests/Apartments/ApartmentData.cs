using Bookify.Domain.Apartments;
using Bookify.Domain.Shared;

namespace Bookify.Domain.UnitTests.Apartments;

internal static class ApartmentData
{
    public static Apartment Create(Money price, Money? cleaningFee = null, ICollection<Amenity>? amenities = null, bool instantBooking = false) =>
        new(Guid.CreateVersion7(),
            Guid.NewGuid(),
            new Name("Apartment 1"),
            new Description("Apartment 1 description"),
            new Address("Country", "State", "ZipCode", "City", "Street"),
            price,
            cleaningFee ?? Money.Zero(),
            amenities?.ToList() ?? [],
            DateTime.UtcNow,
            instantBooking);
}
