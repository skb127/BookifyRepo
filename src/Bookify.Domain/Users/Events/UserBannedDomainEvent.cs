using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users.Events;

public sealed record UserBannedDomainEvent(Guid UserId, string IdentityId) : IDomainEvent;
