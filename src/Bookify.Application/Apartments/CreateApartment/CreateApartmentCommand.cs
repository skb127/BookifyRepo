using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Apartments.CreateApartment;

public record CreateApartmentCommand(
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
    IReadOnlyList<int> Amenities,
    Guid? CancellationPolicyId = null,
    int MinimumNights = 1,
    int CheckInCutOffHours = 3,
    bool InstantBooking = false) : ICommand<Guid>;
