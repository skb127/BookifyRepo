using Bookify.Api.Controllers.Apartments;

namespace Bookify.Application.IntegrationTests.Apartments;

public static class ApartmentData
{
    public const string TestCity_SearchFilter = "TestCity_SearchFilter_UniqueStr";

    public static readonly CreateApartmentRequest ValidCreateApartmentRequest = new(
        "Apartment 1",
        "Description of Apartment 1",
        new AddressRequest("Country", "State", "ZipCode", "City", "Street"),
        new MoneyRequest(100.0m, "USD"),
        new MoneyRequest(50.0m, "USD"),
        [],
        false);

    // GardenView (10) + Parking (3) = 6% upcharge
    public static readonly CreateApartmentRequest ValidCreateApartmentWithAmenitiesRequest = new(
        "Apartment With Amenities",
        "Description of Apartment With Amenities",
        new AddressRequest("Country", "State", "ZipCode", "City", "Street"),
        new MoneyRequest(100.0m, "USD"),
        new MoneyRequest(50.0m, "USD"),
        [10, 3],
        false);

    public static readonly CreateApartmentRequest ValidCreateApartmentInstantBookingRequest = new(
        "Apartment Instant Booking",
        "Description of Apartment Instant Booking",
        new AddressRequest("Country", "State", "ZipCode", "City", "Street"),
        new MoneyRequest(100.0m, "USD"),
        new MoneyRequest(50.0m, "USD"),
        [],
        true);

    public static readonly UpdateApartmentRequest ValidUpdateApartmentRequest = new(
        "Updated Apartment",
        "Updated description",
        new AddressRequest("Spain", "Madrid", "28001", "Madrid", "Gran Vía 12"),
        new MoneyRequest(175.0m, "EUR"),
        new MoneyRequest(35.0m, "EUR"),
        [],
        false);
}
