using Bookify.Api.Controllers.Apartments;

namespace Bookify.Application.IntegrationTests.Apartments;

public static class ApartmentData
{
    public static readonly CreateApartmentRequest ValidCreateApartmentRequest = new(
        "Apartment 1",
        "Description of Apartment 1",
        new AddressRequest("Country", "State", "ZipCode", "City", "Street"),
        new MoneyRequest(100.0m, "USD"),
        new MoneyRequest(50.0m, "USD"),
        []);

    // GardenView (10) + Parking (3) = 6% upcharge
    public static readonly CreateApartmentRequest ValidCreateApartmentWithAmenitiesRequest = new(
        "Apartment With Amenities",
        "Description of Apartment With Amenities",
        new AddressRequest("Country", "State", "ZipCode", "City", "Street"),
        new MoneyRequest(100.0m, "USD"),
        new MoneyRequest(50.0m, "USD"),
        [10, 3]);
}
