using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users.Events;

public sealed record UserPasswordResetDomainEvent(Guid UserId) : IDomainEvent;
