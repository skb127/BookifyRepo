namespace Bookify.Infrastructure.Outbox;

internal sealed record OutboxMessagesResponse(Guid Id, string Content);
