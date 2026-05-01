using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Apartments.Events;

public sealed record ApartmentDeletedDomainEvent(Guid ApartmentId) : IDomainEvent;
