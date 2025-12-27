namespace Bookify.Domain.Bookings;
public enum BookingStatus
{
    None = 0,
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1700:Do not name enum values 'Reserved'", Justification = "Reserved is a valid status used throughout the application.")]
    Reserved = 1,
    Confirmed = 2,
    Rejected = 3,
    Cancelled = 4,
    Completed = 5
}
