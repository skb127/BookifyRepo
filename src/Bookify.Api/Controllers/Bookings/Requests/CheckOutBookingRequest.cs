using Bookify.Domain.Bookings;

namespace Bookify.Api.Controllers.Bookings.Requests;

public sealed record CheckOutBookingRequest(
    ReasonType? Type = null,
    string? Description = null,
    DateOnly? GuestCheckOutDate = null);
