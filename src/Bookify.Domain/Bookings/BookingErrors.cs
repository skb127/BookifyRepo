using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Bookings;
public static class BookingErrors
{
    public static Error NotFound { get; } = new(
        "Booking.Found",
        "The booking with the specified identifier was not found");

    public static Error Overlap { get; } = new(
        "Booking.Overlap",
        "The current booking is overlapping with an existing one");

    public static Error NotReserved { get; } = new(
        "Booking.NotReserved",
        "The booking is not pending");

    public static Error NotConfirmed { get; } = new(
        "Booking.NotConfirmed",
        "The booking is not confirmed");

    public static Error AlreadyStarted { get; } = new(
        "Booking.AlreadyStarted",
        "The booking has already started");
}
