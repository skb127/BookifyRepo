using Bookify.Application.Common;

namespace Bookify.Application.Apartments.GetApartment;

public sealed class ApartmentResponse
{
    public Guid Id { get; init; }
    public Guid OwnerId { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public MoneyResponse Price { get; set; } = null!;
    public MoneyResponse CleaningFee { get; set; } = null!;
    public AddressResponse Address { get; set; } = null!;
    public IReadOnlyList<int> Amenities { get; init; } = [];
    public DateTime? LastBookedOnUtc { get; init; }
    public bool InstantBooking { get; init; }
}

