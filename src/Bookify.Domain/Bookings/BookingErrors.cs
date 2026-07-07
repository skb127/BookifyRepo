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

    public static Error NotPendingPayment { get; } = new(
        "Booking.NotPendingPayment",
        "The booking is not in PendingPayment status");

    public static Error RefundNotEligible { get; } = new(
        "Booking.RefundNotEligible",
        "The booking is not eligible for a refund");

    public static Error NotExpirable { get; } = new(
        "Booking.NotExpirable",
        "The booking cannot be expired in its current status");

    public static Error NoPolicyAvailable { get; } = new(
        "Booking.NoPolicyAvailable",
        "No cancellation policy is available");

    public static Error NotCancellable { get; } = new(
        "Booking.NotCancellable",
        "The booking cannot be cancelled in its current status");

    public static Error BelowMinimumNights { get; } = new(
        "Booking.BelowMinimumNights",
        "The booking duration is below the minimum nights required for this apartment");

    public static Error CheckInTooSoon { get; } = new(
        "Booking.CheckInTooSoon",
        "The check-in date is too soon, breaking the cut-off hours rule");

    public static Error CheckInTooEarly { get; } = new(
        "Booking.CheckInTooEarly",
        "The check-in date is earlier than the booking start date");

    public static Error StayNotYetEnded { get; } = new(
        "Booking.StayNotYetEnded",
        "The stay has not yet ended");

    public static Error InvalidCheckInDate { get; } = new(
        "Booking.InvalidCheckInDate",
        "The provided check-in date is invalid for this booking");

    public static Error InvalidCheckOutDate { get; } = new(
        "Booking.InvalidCheckOutDate",
        "The provided check-out date is invalid for this booking");
}
