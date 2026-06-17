using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Bookings.ExpireCheckoutSession;

public sealed record ExpireCheckoutSessionCommand(Guid BookingId) : ICommand;
