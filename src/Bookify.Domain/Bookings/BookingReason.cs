namespace Bookify.Domain.Bookings;

/// <summary>
/// Represents a reason associated with a booking lifecycle transition.
/// </summary>
public sealed record BookingReason
{
    private BookingReason() { } // For EF Core

    public ReasonType Type { get; private init; }
    public string? Description { get; private init; }
    public DateTime CreatedOnUtc { get; private init; }

    public static BookingReason Create(ReasonType type, string? description, DateTime utcNow)
    {
        if (description?.Length > 500)
        {
            throw new ArgumentException("Description cannot exceed 500 characters.", nameof(description));
        }

        return new BookingReason
        {
            Type = type,
            Description = description,
            CreatedOnUtc = utcNow
        };
    }
}
