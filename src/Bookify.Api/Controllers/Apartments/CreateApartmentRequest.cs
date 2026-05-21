using System.Text.Json.Serialization;

namespace Bookify.Api.Controllers.Apartments;

public sealed record MoneyRequest([property: JsonRequired] decimal Amount, string Currency);

public sealed record AddressRequest(
    string Country,
    string State,
    string ZipCode,
    string City,
    string Street);

public sealed record CreateApartmentRequest(
    string Name,
    string Description,
    AddressRequest Address,
    MoneyRequest Price,
    MoneyRequest CleaningFee,
    IReadOnlyList<int> Amenities,
    bool InstantBooking = false);

public sealed record UpdateApartmentRequest(
    string Name,
    string Description,
    AddressRequest Address,
    MoneyRequest Price,
    MoneyRequest CleaningFee,
    IReadOnlyList<int> Amenities,
    bool InstantBooking = false);
