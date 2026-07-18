using Bookify.Domain.Apartments;
using Bookify.Domain.Shared;

namespace Bookify.Domain.UnitTests.Apartments;

internal static class ApartmentData
{
    public static Apartment Create(
        Money price,
        Money? cleaningFee = null,
        ICollection<Amenity>? amenities = null,
        bool instantBooking = false,
        int baseGuests = 1,
        int maxGuests = 1,
        Money? extraGuestFee = null) =>
        new(Guid.CreateVersion7(),
            Guid.NewGuid(),
            new Name("Apartment 1"),
            new Description("Apartment 1 description"),
            new Address("Country", "State", "ZipCode", "City", "Street"),
            price,
            cleaningFee ?? Money.Zero(price.Currency),
            amenities?.ToList() ?? [],
            DateTime.UtcNow,
            instantBooking,
            null,
            1,
            3,
            baseGuests,
            maxGuests,
            extraGuestFee ?? Money.Zero(price.Currency));
}
