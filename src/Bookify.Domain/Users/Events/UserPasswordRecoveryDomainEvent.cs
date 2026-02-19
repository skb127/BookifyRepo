using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users.Events;

public sealed record UserPasswordRecoveryDomainEvent(Guid UserId, string Token) : IDomainEvent;
