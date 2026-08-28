namespace Bookify.Api.Controllers.Bookings.Requests;

public sealed record CloseStayRequest(DateOnly? CheckInDate, DateOnly? CheckOutDate);
