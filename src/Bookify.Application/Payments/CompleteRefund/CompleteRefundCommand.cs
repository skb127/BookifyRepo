using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Payments.CompleteRefund;

public record CompleteRefundCommand(
    Guid BookingId,
    string StripeRefundId,
    decimal Amount) : ICommand;
