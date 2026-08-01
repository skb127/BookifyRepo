using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users.Events;

public sealed record UserAccountDeletionRequestedDomainEvent(Guid UserId, string RawToken, DateTime DeletionScheduledAt) : IDomainEvent;
