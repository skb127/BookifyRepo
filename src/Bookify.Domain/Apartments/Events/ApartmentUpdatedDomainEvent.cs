using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Apartments.Events;

public sealed record ApartmentUpdatedDomainEvent(Guid ApartmentId) : IDomainEvent;
