using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users.Events;

public sealed record UserUnbannedDomainEvent(Guid UserId, string IdentityId) : IDomainEvent;
