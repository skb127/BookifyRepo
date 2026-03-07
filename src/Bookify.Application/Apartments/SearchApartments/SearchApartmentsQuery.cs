using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Common;

namespace Bookify.Application.Apartments.SearchApartments;

public record SearchApartmentsQuery(
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? City,
    string? Country,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? Currency,
    IReadOnlyList<int>? Amenities,
    int Page,
    int PageSize) : IQuery<PagedResponse<ApartmentResponse>>, IPagedQuery;
