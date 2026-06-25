using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Payments.ExpireCheckoutSession;

public record ExpireStripeSessionCommand(
    Guid BookingId,
    string StripeSessionId) : ICommand;
