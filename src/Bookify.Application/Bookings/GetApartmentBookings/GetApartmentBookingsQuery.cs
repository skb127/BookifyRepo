using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Bookings.GetBookings;
using Bookify.Application.Common;

namespace Bookify.Application.Bookings.GetApartmentBookings;

public sealed record GetApartmentBookingsQuery(
    Guid ApartmentId,
    int? Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int Page,
    int PageSize) : IQuery<PagedResponse<BookingSummaryResponse>>, IPagedQuery;
