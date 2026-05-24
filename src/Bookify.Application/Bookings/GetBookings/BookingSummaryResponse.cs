namespace Bookify.Application.Bookings.GetBookings;

public sealed class BookingSummaryResponse
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public Guid ApartmentId { get; init; }

    public int Status { get; init; }

    public decimal TotalPriceAmount { get; init; }

    public string TotalPriceCurrency { get; init; } = "";

    public DateOnly DurationStart { get; init; }

    public DateOnly DurationEnd { get; init; }

    public DateTime CreatedOnUtc { get; init; }

    public int PaymentStatus { get; init; }

    public DateTime? ExpiresAt { get; init; }
}
