namespace Bookify.Application.Abstractions.Payments;

public record RefundResult(
    string RefundId,
    string Status,
    decimal Amount);
