using Bookify.Domain.Apartments;
using Bookify.Domain.Shared;

namespace Bookify.Application.UnitTests.Apartment;

internal static class ApartmentData
{
    public static Domain.Apartments.Apartment Create() =>
        new(Guid.CreateVersion7(),
            new Name("Apartment 1"),
            new Description("Apartment 1 description"),
            new Address("Country", "State", "ZipCode", "City", "Street"),
            new Money(50.0m, Currency.Usd),
            Money.Zero(),
            []);
}
