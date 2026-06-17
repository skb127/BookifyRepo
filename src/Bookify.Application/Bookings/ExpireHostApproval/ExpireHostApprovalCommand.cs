using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Bookings.ExpireHostApproval;

public sealed record ExpireHostApprovalCommand(Guid BookingId) : ICommand;
