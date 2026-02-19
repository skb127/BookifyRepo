using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users.Events;

public sealed record UserProfileUpdatedDomainEvent(Guid UserId) : IDomainEvent;
