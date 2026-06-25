using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Payments.FailRefund;

public record FailRefundCommand(
    Guid BookingId,
    string StripeRefundId,
    string FailureReason) : ICommand;
