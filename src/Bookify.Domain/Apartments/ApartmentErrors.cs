using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Apartments;

public static class ApartmentErrors
{
    public static readonly Error NotFound = new(
        "Apartment.NotFound",
        "The apartment with the specified identifier was not found");

    public static readonly Error InvalidCurrency = new(
        "Apartment.InvalidCurrency",
        "The provided currency is invalid");

    public static readonly Error HasActiveBookings = new(
        "Apartment.HasActiveBookings",
        "The apartment has active bookings");
}
