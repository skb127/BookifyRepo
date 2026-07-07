namespace Bookify.Api.Controllers.Bookings;

public sealed record CloseStayRequest(DateOnly? CheckInDate, DateOnly? CheckOutDate);
