namespace Bookify.Api.Controllers.Bookings.Requests;

public sealed record CheckInBookingRequest(DateOnly? GuestCheckInDate = null);
