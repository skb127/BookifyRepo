namespace Bookify.Domain.Apartments;

public record Address(
    string County,
    string State,
    string ZipCode,
    string City,
    string Street);
