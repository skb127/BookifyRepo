using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users.Events;

public sealed record UserEmailChangedDomainEvent(Guid UserId, string OldEmail, string NewEmail) : IDomainEvent;
