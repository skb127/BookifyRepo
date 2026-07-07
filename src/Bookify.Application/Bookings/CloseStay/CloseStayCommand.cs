using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Bookings.CloseStay;

public sealed record CloseStayCommand(
    Guid BookingId,
    DateOnly CheckInDate,
    DateOnly CheckOutDate) : ICommand;
