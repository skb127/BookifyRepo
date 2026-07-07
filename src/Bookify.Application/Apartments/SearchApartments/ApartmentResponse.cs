using Bookify.Application.Common;

namespace Bookify.Application.Apartments.SearchApartments;

public sealed class ApartmentResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = "";

    public string Description { get; init; } = "";

    public MoneyResponse Price { get; init; } = null!;

    public MoneyResponse CleaningFee { get; init; } = null!;

    public IReadOnlyList<int> Amenities { get; init; } = [];

    public bool IsAvailable { get; init; }

    public double AverageRating { get; init; }

    public AddressResponse Address { get; set; } = null!;

    public int MinimumNights { get; init; }

    public int CheckInCutOffHours { get; init; }
}
