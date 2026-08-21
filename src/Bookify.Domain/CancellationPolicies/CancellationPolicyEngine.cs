using Bookify.Domain.Bookings;

namespace Bookify.Domain.CancellationPolicies;

public sealed class CancellationPolicyEngine
{
    public PenaltyResult CalculatePenalty(
        Booking booking,
        CancellationPolicy policy,
        DateTime utcNow,
        bool cancelledByHost)
    {
        decimal totalPrice = booking.TotalPrice.Amount + booking.Taxes.Sum(t => t.CalculatedAmount.Amount);
        string currency = booking.TotalPrice.Currency.Code;

        // Convert the check-in DateOnly to a DateTime at midnight UTC to calculate elapsed hours.
        var checkInDateTime = booking.Duration.Start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        double hoursUntilCheckin = (checkInDateTime - utcNow).TotalHours;

        bool isLate = hoursUntilCheckin < policy.ThresholdHours;

        decimal guestPenaltyAmount = 0m;
        decimal hostPenaltyAmount = 0m;
        decimal refundAmount;

        if (!cancelledByHost)
        {
            // Guest initiated cancellation
            decimal rate = isLate ? policy.LateGuestPenaltyRate : policy.EarlyGuestPenaltyRate;
            guestPenaltyAmount = Math.Round(totalPrice * rate, 2);
            refundAmount = totalPrice - guestPenaltyAmount;
        }
        else
        {
            // Host initiated cancellation
            decimal rate = isLate ? policy.LateHostPenaltyRate : policy.EarlyHostPenaltyRate;
            hostPenaltyAmount = Math.Round(totalPrice * rate, 2);
            // Stripe refund is capped at 100% of TotalPrice because Stripe does not allow refunding more than the original captured amount.
            // The HostPenaltyAmount is registered in HostBalance as a debt/compensation, but the actual Stripe refund is capped at TotalPrice.
            refundAmount = totalPrice;
        }

        bool requiresRefund = refundAmount > 0;

        return new PenaltyResult(
            guestPenaltyAmount,
            hostPenaltyAmount,
            refundAmount,
            currency,
            requiresRefund);
    }
}
