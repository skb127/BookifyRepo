using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments.Events;
using Bookify.Domain.Shared;

namespace Bookify.Domain.Apartments;

public sealed class Apartment : Entity
{
    private List<Amenity> _amenities = [];

    public Apartment(
        Guid id,
        Guid ownerId,
        Name name,
        Description description,
        Address address,
        Money price,
        Money cleaningFee,
        IReadOnlyCollection<Amenity> amenities,
        DateTime createdOnUtc,
        bool instantBooking,
        Guid? cancellationPolicyId,
        int minimumNights,
        int checkInCutOffHours,
        int baseGuests = 1,
        int maxGuests = 1,
        Money? extraGuestFee = null)
        : base(id)
    {
        OwnerId = ownerId;
        Name = name;
        Description = description;
        Address = address;
        Price = price;
        CleaningFee = cleaningFee;
        _amenities = new List<Amenity>(amenities ?? []);
        CreatedOnUtc = createdOnUtc;
        InstantBooking = instantBooking;
        CancellationPolicyId = cancellationPolicyId;
        MinimumNights = minimumNights;
        CheckInCutOffHours = checkInCutOffHours;
        BaseGuests = baseGuests;
        MaxGuests = maxGuests;
        ExtraGuestFee = extraGuestFee ?? Money.Zero(price.Currency);
    }

    /// <summary>
    /// Initializes a new instance of the Apartment class. This constructor is intended for internal use and prevents
    /// external instantiation.
    /// </summary>
    private Apartment()
    {

    }

    public Guid OwnerId { get; private set; }
    public Name Name { get; private set; } = null!;
    public Description Description { get; private set; } = null!;
    public Address Address { get; private set; } = null!;
    public Money Price { get; private set; } = null!;
    public Money CleaningFee { get; private set; } = null!;
    public DateTime? LastBookedOnUtc { get; internal set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? EditedOnUtc { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public bool InstantBooking { get; private set; }
    public Guid? CancellationPolicyId { get; private set; }
    public int MinimumNights { get; private set; } = 1;
    public int CheckInCutOffHours { get; private set; } = 3;
    public int BaseGuests { get; private set; } = 1;
    public int MaxGuests { get; private set; } = 1;
    public Money ExtraGuestFee { get; private set; } = null!;
    public IReadOnlyList<Amenity> Amenities
    {
        get => _amenities.AsReadOnly();
#pragma warning disable S1144 // Unused private types or members should be removed - Used by EF Core
        private set => _amenities = [.. value ?? []];
#pragma warning restore S1144
    }

    public static Apartment Create(
        Guid ownerId,
        Name name,
        Description description,
        Address address,
        Money price,
        Money cleaningFee,
        IReadOnlyCollection<Amenity> amenities,
        DateTime utcNow,
        bool instantBooking = false,
        Guid? cancellationPolicyId = null,
        int minimumNights = 1,
        int checkInCutOffHours = 3,
        int baseGuests = 1,
        int maxGuests = 1,
        Money? extraGuestFee = null)
    {
        var apartment = new Apartment(
            Guid.CreateVersion7(),
            ownerId,
            name,
            description,
            address,
            price,
            cleaningFee,
            amenities,
            utcNow,
            instantBooking,
            cancellationPolicyId,
            minimumNights,
            checkInCutOffHours,
            baseGuests,
            maxGuests,
            extraGuestFee);

        return apartment;
    }

    public void Update(
        Name name,
        Description description,
        Address address,
        Money price,
        Money cleaningFee,
        IReadOnlyCollection<Amenity> amenities,
        DateTime utcNow,
        bool instantBooking,
        Guid? cancellationPolicyId,
        int minimumNights,
        int checkInCutOffHours,
        int baseGuests = 1,
        int maxGuests = 1,
        Money? extraGuestFee = null)
    {
        Money resolvedExtraGuestFee = extraGuestFee ?? Money.Zero(price.Currency);

        Name = name;
        Description = description;
        Address = address;
        Price = price;
        CleaningFee = cleaningFee;
        EditedOnUtc = utcNow;
        InstantBooking = instantBooking;
        CancellationPolicyId = cancellationPolicyId;
        MinimumNights = minimumNights;
        CheckInCutOffHours = checkInCutOffHours;
        BaseGuests = baseGuests;
        MaxGuests = maxGuests;
        ExtraGuestFee = resolvedExtraGuestFee;

        _amenities = new List<Amenity>(amenities ?? []);

        RaiseDomainEvent(new ApartmentUpdatedDomainEvent(Id));
    }

    public void Delete(DateTime utcNow)
    {
        DeletedAt = utcNow;

        RaiseDomainEvent(new ApartmentDeletedDomainEvent(Id));
    }
}
