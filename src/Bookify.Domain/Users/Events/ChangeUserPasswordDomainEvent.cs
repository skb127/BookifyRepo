using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users.Events;

public sealed record ChangeUserPasswordDomainEvent(Guid UserId) : IDomainEvent;
