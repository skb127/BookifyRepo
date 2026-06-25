using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Payments.ConfirmPayment;

public record ConfirmPaymentCommand(
    Guid BookingId,
    string StripeSessionId,
    string StripePaymentIntentId,
    bool IsInstantBooking) : ICommand;
