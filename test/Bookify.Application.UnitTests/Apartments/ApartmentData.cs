using Bookify.Domain.Apartments;
using Bookify.Domain.Shared;

namespace Bookify.Application.UnitTests.Apartments;

internal static class ApartmentData
{
    public static Apartment Create() =>
        new(Guid.CreateVersion7(),
            Guid.NewGuid(),
            new Name("Apartment 1"),
            new Description("Apartment 1 description"),
            new Address("Country", "State", "ZipCode", "City", "Street"),
            new Money(50.0m, Currency.Usd),
            Money.Zero(),
            [],
            DateTime.UtcNow,
            false,
            null,
            1,
            3);

    public static Apartment CreateWithInstantBooking() =>
        new(Guid.CreateVersion7(),
            Guid.NewGuid(),
            new Name("Apartment Instant Booking"),
            new Description("Apartment Instant Booking description"),
            new Address("Country", "State", "ZipCode", "City", "Street"),
            new Money(50.0m, Currency.Usd),
            Money.Zero(),
            [],
            DateTime.UtcNow,
            true,
            null,
            1,
            3);
}