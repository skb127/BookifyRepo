using System.Data;
using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Common;
using Bookify.Domain.Abstractions;
using Dapper;

namespace Bookify.Application.Bookings.GetUserBookings;

internal sealed class GetUserBookingsQueryHandler
    : IQueryHandler<GetUserBookingsQuery, PagedResponse<UserBookingResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly IUserContext _userContext;

    public GetUserBookingsQueryHandler(
        ISqlConnectionFactory sqlConnectionFactory,
        IUserContext userContext)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
        _userContext = userContext;
    }

    public async Task<Result<PagedResponse<UserBookingResponse>>> Handle(
        GetUserBookingsQuery request,
        CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        var conditions = new List<string> { "user_id = @UserId" };
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

        string where = string.Join(" AND ", conditions);
        int offset = (request.Page - 1) * request.PageSize;

        IEnumerable<UserBookingResponse> dataTask = await connection.QueryAsync<UserBookingResponse>(
            $"""
            SELECT
                id AS Id,
                apartment_id AS ApartmentId,
                status AS Status,
                total_price_amount AS TotalPriceAmount,
                total_price_currency AS TotalPriceCurrency,
                duration_start AS DurationStart,
                duration_end AS DurationEnd,
                created_on_utc AS CreatedOnUtc,
                payment_status AS PaymentStatus,
                expires_at AS ExpiresAt,
                guest_count AS GuestCount
            FROM bookings
            WHERE {where}
            ORDER BY created_on_utc DESC
            LIMIT @PageSize OFFSET @Offset
            """,
            new
            {
                _userContext.UserId,
                request.Status,
                request.StartDate,
                request.EndDate,
                request.PageSize,
                Offset = offset
            });

        int countTask = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM bookings WHERE {where}",
            new
            {
                _userContext.UserId,
                request.Status,
                request.StartDate,
                request.EndDate
            });

        return new PagedResponse<UserBookingResponse>
        {
            Items = [.. dataTask],
            TotalCount = countTask,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
