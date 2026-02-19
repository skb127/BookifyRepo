using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users.Events;

public sealed record UserEmailChangeInitiatedDomainEvent(
    Guid UserId,
    string Token,
    string NewEmail,
    string CurrentEmail) : IDomainEvent;
