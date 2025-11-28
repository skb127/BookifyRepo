using Bookify.Domain.Apartments;
using Bookify.Domain.Shared;

namespace Bookify.Domain.UnitTests.Apartments;

internal static class ApartmentData
{
    public static Apartment Create(Money price, Money? cleaningFee = null) =>
        new(Guid.CreateVersion7(),
            new Name("Apartment 1"),
            new Description("Apartment 1 description"),
            new Address("Country", "State", "ZipCode", "City", "Street"),
            price,
            cleaningFee ?? Money.Zero(),
            []);
}
