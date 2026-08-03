using Bookify.Domain.Bookings;

namespace Bookify.Api.Controllers.Bookings;

public sealed record CheckOutBookingRequest(
    ReasonType? Type = null,
    string? Description = null,
    DateOnly? GuestCheckOutDate = null);
