namespace Bookify.Domain.Bookings;

/// <summary>
/// Represents the status of a booking.
/// </summary>
public enum BookingStatus
{
    None = 0,

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1700:Do not name enum values 'Reserved'",
        Justification = "Reserved is a valid status used throughout the application.")]
    Reserved = 1,
    PendingPayment = 2,
    Confirmed = 3,
    Rejected = 4,
    Cancelled = 5,
    InProgress = 6,
    Completed = 7,
    Expired = 8,
    NoShow = 9
}
