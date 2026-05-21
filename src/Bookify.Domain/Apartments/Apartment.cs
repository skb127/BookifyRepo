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
        bool instantBooking)
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
        bool instantBooking = false)
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
            instantBooking);

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
        bool instantBooking)
    {
        Name = name;
        Description = description;
        Address = address;
        Price = price;
        CleaningFee = cleaningFee;
        EditedOnUtc = utcNow;
        InstantBooking = instantBooking;

        _amenities = new List<Amenity>(amenities ?? []);

        RaiseDomainEvent(new ApartmentUpdatedDomainEvent(Id));
    }

    public void Delete(DateTime utcNow)
    {
        DeletedAt = utcNow;

        RaiseDomainEvent(new ApartmentDeletedDomainEvent(Id));
    }
}
