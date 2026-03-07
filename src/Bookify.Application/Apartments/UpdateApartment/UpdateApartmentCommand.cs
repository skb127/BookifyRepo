using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Apartments.UpdateApartment;

public record UpdateApartmentCommand(
    Guid Id,
    string Name,
    string Description,
    string Country,
    string State,
    string ZipCode,
    string City,
    string Street,
    decimal PriceAmount,
    string PriceCurrency,
    decimal CleaningFeeAmount,
    string CleaningFeeCurrency,
    IReadOnlyList<int> Amenities) : ICommand;
