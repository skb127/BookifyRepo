using System.Data;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Common;
using Bookify.Domain.Abstractions;
using Dapper;

namespace Bookify.Application.Bookings.GetBookings;

internal sealed class GetBookingsQueryHandler
    : IQueryHandler<GetBookingsQuery, PagedResponse<BookingSummaryResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetBookingsQueryHandler(ISqlConnectionFactory sqlConnectionFactory) =>
        _sqlConnectionFactory = sqlConnectionFactory;

    public async Task<Result<PagedResponse<BookingSummaryResponse>>> Handle(
        GetBookingsQuery request,
        CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        var conditions = new List<string>();

        if (request.UserId.HasValue)
        {
            conditions.Add("user_id = @UserId");
        }

        if (request.ApartmentId.HasValue)
        {
            conditions.Add("apartment_id = @ApartmentId");
        }

        if (request.Status.HasValue)
        {
            conditions.Add("status = @Status");
        }

        if (request.StartDate.HasValue)
        {
            conditions.Add("duration_start >= @StartDate");
        }

        if (request.EndDate.HasValue)
        {
            conditions.Add("duration_end <= @EndDate");
        }

        string where = conditions.Count > 0
            ? $"WHERE {string.Join(" AND ", conditions)}"
            : string.Empty;

        int offset = (request.Page - 1) * request.PageSize;

        IEnumerable<BookingSummaryResponse> dataTask = await connection.QueryAsync<BookingSummaryResponse>(
            $"""
            SELECT
                id AS Id,
                user_id AS UserId,
                apartment_id AS ApartmentId,
                status AS Status,
                total_price_amount AS TotalPriceAmount,
                total_price_currency AS TotalPriceCurrency,
                duration_start AS DurationStart,
                duration_end AS DurationEnd,
                created_on_utc AS CreatedOnUtc
            FROM bookings
            {where}
            ORDER BY created_on_utc DESC
            LIMIT @PageSize OFFSET @Offset
            """,
            new
            {
                request.UserId,
                request.ApartmentId,
                request.Status,
                request.StartDate,
                request.EndDate,
                request.PageSize,
                Offset = offset
            });

        int countTask = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM bookings {where}",
            new
            {
                request.UserId,
                request.ApartmentId,
                request.Status,
                request.StartDate,
                request.EndDate
            });

        return new PagedResponse<BookingSummaryResponse>
        {
            Items = [.. dataTask],
            TotalCount = countTask,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
