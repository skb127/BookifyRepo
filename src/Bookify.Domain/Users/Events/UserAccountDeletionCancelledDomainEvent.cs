using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users.Events;

public sealed record UserAccountDeletionCancelledDomainEvent(Guid UserId) : IDomainEvent;
