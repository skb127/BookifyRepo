using Bookify.Domain.Apartments;
using Bookify.Domain.Shared;

namespace Bookify.Application.UnitTests.Apartments;

internal static class ApartmentData
{
    public static Apartment Create(
        int baseGuests = 1,
        int maxGuests = 1,
        Money? extraGuestFee = null) =>
        new(Guid.CreateVersion7(),
            Guid.NewGuid(),
            new Name("Apartment 1"),
            new Description("Apartment 1 description"),
            new Address("Country", "State", "ZipCode", "City", "Street"),
            new Money(50.0m, Currency.Usd),
            Money.Zero(Currency.Usd),
            [],
            DateTime.UtcNow,
            false,
            null,
            1,
            3,
            baseGuests,
            maxGuests,
            extraGuestFee ?? Money.Zero(Currency.Usd));

    public static Apartment CreateWithInstantBooking(
        int baseGuests = 1,
        int maxGuests = 1,
        Money? extraGuestFee = null) =>
        new(Guid.CreateVersion7(),
            Guid.NewGuid(),
            new Name("Apartment Instant Booking"),
            new Description("Apartment Instant Booking description"),
            new Address("Country", "State", "ZipCode", "City", "Street"),
            new Money(50.0m, Currency.Usd),
            Money.Zero(Currency.Usd),
            [],
            DateTime.UtcNow,
            true,
            null,
            1,
            3,
            baseGuests,
            maxGuests,
            extraGuestFee ?? Money.Zero(Currency.Usd));
}