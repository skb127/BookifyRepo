using Bookify.Api.Controllers.Apartments.Requests;

namespace Bookify.Application.IntegrationTests.Apartments;

public static class ApartmentData
{
    public const string TestCitySearchFilter = "TestCity_SearchFilter_UniqueStr";

    public static readonly CreateApartmentRequest ValidCreateApartmentRequest = new(
        "Apartment 1",
        "Description of Apartment 1",
        new AddressRequest("Country", "State", "ZipCode", "City", "Street"),
        new MoneyRequest(100.0m, "USD"),
        new MoneyRequest(50.0m, "USD"),
        []);

    public static CreateApartmentRequest CreateWithUniqueCity(string prefix = "City") => new(
        "Apartment " + Guid.CreateVersion7(),
        "Description of Apartment",
        new AddressRequest("Country", "State", "ZipCode", $"{prefix}_{Guid.CreateVersion7()}", "Street"),
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

    public static readonly CreateApartmentRequest ValidCreateApartmentInstantBookingRequest = new(
        "Apartment Instant Booking",
        "Description of Apartment Instant Booking",
        new AddressRequest("Country", "State", "ZipCode", "City", "Street"),
        new MoneyRequest(100.0m, "USD"),
        new MoneyRequest(50.0m, "USD"),
        [],
        null,
        1,
        3,
        true);

    public static readonly UpdateApartmentRequest ValidUpdateApartmentRequest = new(
        "Updated Apartment",
        "Updated description",
        new AddressRequest("Spain", "Madrid", "28001", "Madrid", "Gran Vía 12"),
        new MoneyRequest(175.0m, "EUR"),
        new MoneyRequest(35.0m, "EUR"),
        []);
    public static readonly CreateApartmentRequest InvoiceTestApartmentRequest = new(
        "Invoice Test Apartment",
        "Apartment for invoice generation integration tests",
        new AddressRequest("ES", "Madrid", "28001", "Madrid", "Gran Vía 12"),
        new MoneyRequest(100.0m, "USD"),
        new MoneyRequest(25.0m, "USD"),
        [10, 3],
        null,
        1,
        3,
        false,
        2,
        4,
        new MoneyRequest(15.0m, "USD"));

    public static readonly CreateApartmentRequest InvoiceTestInstantApartmentRequest = new(
        "Invoice Test Instant Apartment",
        "Apartment for instant invoice generation tests",
        new AddressRequest("ES", "Madrid", "28001", "Madrid", "Gran Vía 12"),
        new MoneyRequest(100.0m, "USD"),
        new MoneyRequest(25.0m, "USD"),
        [10, 3],
        null,
        1,
        3,
        true,
        2,
        4,
        new MoneyRequest(15.0m, "USD"));
}
