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
    IReadOnlyList<int> Amenities,
    Guid? CancellationPolicyId = null,
    int MinimumNights = 1,
    int CheckInCutOffHours = 3,
    bool InstantBooking = false,
    int BaseGuests = 1,
    int MaxGuests = 1,
    decimal ExtraGuestFeeAmount = 0,
    string ExtraGuestFeeCurrency = "USD") : ICommand;
