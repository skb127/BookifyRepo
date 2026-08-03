namespace Bookify.Api.Controllers.Apartments.Requests;

public sealed record AddressRequest(
    string Country,
    string State,
    string ZipCode,
    string City,
    string Street);
