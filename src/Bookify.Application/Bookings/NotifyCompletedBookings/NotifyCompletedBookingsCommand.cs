using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Bookings.NotifyCompletedBookings;

public sealed record NotifyCompletedBookingsCommand(int BatchSize = 50) : ICommand;
