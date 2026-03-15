using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Reviews.Events;

public sealed record ReviewUpdatedDomainEvent(Guid ReviewId, Guid ApartmentId, Guid BookingId) : IDomainEvent;
