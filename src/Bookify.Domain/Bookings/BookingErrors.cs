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

    public static Error Unauthorized { get; } = new(
        "Booking.Unauthorized",
        "You do not have permission to perform this action on this booking");

    public static Error NotInProgress { get; } = new(
        "Booking.NotInProgress",
        "The booking is not currently in progress");

    public static Error CannotMarkNoShow { get; } = new(
        "Booking.CannotMarkNoShow",
        "The booking cannot be marked as a no-show because the start date has not been reached yet or is invalid");

    public static Error AlreadyExpired { get; } = new(
        "Booking.AlreadyExpired",
        "The booking has already expired");

    public static Error AlreadyCheckedIn { get; } = new(
        "Booking.AlreadyCheckedIn",
        "The booking has already been checked in");
}
