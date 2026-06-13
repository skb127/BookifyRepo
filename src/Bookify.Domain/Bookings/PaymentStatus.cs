namespace Bookify.Domain.Bookings;

/// <summary>
/// Represents the payment status of a booking.
/// </summary>
public enum PaymentStatus
{
    Unpaid = 0,
    Authorized = 1,
    Paid = 2,
    RefundProcessing = 3,
    Refunded = 4
}
