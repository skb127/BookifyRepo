namespace Bookify.Api.Controllers.Bookings;

public sealed record CheckInBookingRequest(DateOnly? GuestCheckInDate = null);
