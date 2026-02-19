using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users.Events;

public sealed record UserChangedPasswordDomainEvent(Guid UserId) : IDomainEvent;
