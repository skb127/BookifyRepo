namespace Bookify.Application.Bookings.GetCancellationPreview;

public record CancellationPreviewResponse(
    decimal TotalPrice,
    decimal PenaltyAmount,
    decimal RefundAmount,
    string Currency,
    bool RequiresRefund,
    bool IsLateCancellation,
    string CancellationPolicyName,
    bool IsCancelledByHost);
