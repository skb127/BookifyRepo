namespace Bookify.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public OutboxMessage(Guid id, DateTime occurredOnUtc, string type, string content)
    {
        Id = id;
        OccurredOnUtc = occurredOnUtc;
        Type = type;
        Content = content;
    }

    public Guid Id { get; init; } // Unique identifier for the event

    public DateTime OccurredOnUtc { get; init; } // When the event was created

    public string Type { get; init; } // Will hold the fully qualified type name

    public string Content { get; init; } // Serialized JSON string representation of the domain event instance

    public DateTime? ProcessedOnUtc { get; init; } // When the event was processed

    public string? Error { get; init; } // Error message if processing failed
}
