using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Common;

namespace Bookify.Application.Bookings.GetUserBookings;

public sealed record GetUserBookingsQuery(
    int? Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int Page,
    int PageSize) : IQuery<PagedResponse<UserBookingResponse>>, IPagedQuery;
