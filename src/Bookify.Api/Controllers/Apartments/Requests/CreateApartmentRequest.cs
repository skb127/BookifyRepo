namespace Bookify.Api.Controllers.Apartments.Requests;

public sealed record CreateApartmentRequest(
    string Name,
    string Description,
    AddressRequest Address,
    MoneyRequest Price,
    MoneyRequest CleaningFee,
    IReadOnlyList<int> Amenities,
    Guid? CancellationPolicyId = null,
    int MinimumNights = 1,
    int CheckInCutOffHours = 3,
    bool InstantBooking = false,
    int BaseGuests = 1,
    int MaxGuests = 1,
    MoneyRequest? ExtraGuestFee = null);