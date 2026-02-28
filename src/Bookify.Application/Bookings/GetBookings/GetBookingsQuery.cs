using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Common;

namespace Bookify.Application.Bookings.GetBookings;

public sealed record GetBookingsQuery(
    Guid? UserId,
    Guid? ApartmentId,
    int? Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int Page,
    int PageSize) : IQuery<PagedResponse<BookingSummaryResponse>>, IPagedQuery;
