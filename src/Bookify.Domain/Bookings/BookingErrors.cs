using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Bookings;
public static class BookingErrors
{
    // Backing fields kept non-visible to satisfy CA2211 (Non-constant fields should not be visible)
    private static readonly Error s_notFound = new(
        "Booking.Found",
        "The booking with the specified identifier was not found");

    private static readonly Error s_overlap = new(
        "Booking.Overlap",
        "The current booking is overlapping with an existing one");

    private static readonly Error s_notReserved = new(
        "Booking.NotReserved",
        "The booking is not pending");

    private static readonly Error s_notConfirmed = new(
        "Booking.NotConfirmed",
        "The booking is not confirmed");

    private static readonly Error s_alreadyStarted = new(
        "Booking.AlreadyStarted",
        "The booking has already started");

    // Public read-only properties expose the same singleton instances without public static fields
    public static Error NotFound => s_notFound;
    public static Error Overlap => s_overlap;
    public static Error NotReserved => s_notReserved;
    public static Error NotConfirmed => s_notConfirmed;
    public static Error AlreadyStarted => s_alreadyStarted;
}
