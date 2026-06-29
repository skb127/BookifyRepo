namespace Bookify.Domain.CancellationPolicies;

public record PenaltyResult(
    decimal GuestPenaltyAmount,
    decimal HostPenaltyAmount,
    decimal RefundAmount,
    string Currency,
    bool RequiresRefund);