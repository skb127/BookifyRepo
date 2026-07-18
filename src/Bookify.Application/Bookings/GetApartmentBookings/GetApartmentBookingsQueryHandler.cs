using System.Data;
using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Authorization;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Bookings.GetBookings;
using Bookify.Application.Common;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Users;
using Dapper;

namespace Bookify.Application.Bookings.GetApartmentBookings;

internal sealed class GetApartmentBookingsQueryHandler
    : IQueryHandler<GetApartmentBookingsQuery, PagedResponse<BookingSummaryResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly IUserContext _userContext;
    private readonly IAuthorizationService _authorizationService;

    public GetApartmentBookingsQueryHandler(
        ISqlConnectionFactory sqlConnectionFactory,
        IUserContext userContext,
        IAuthorizationService authorizationService)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
        _userContext = userContext;
        _authorizationService = authorizationService;
    }

    public async Task<Result<PagedResponse<BookingSummaryResponse>>> Handle(
        GetApartmentBookingsQuery request,
        CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        // 1. Verify Apartment exists and get its OwnerId
        Guid? ownerId = await connection.QueryFirstOrDefaultAsync<Guid?>(
            "SELECT owner_id FROM apartments WHERE id = @ApartmentId AND deleted_at IS NULL",
            new { request.ApartmentId });

        if (!ownerId.HasValue)
        {
            return Result.Failure<PagedResponse<BookingSummaryResponse>>(ApartmentErrors.NotFound);
        }

        // 2. Authorization (Admin or Owner)
        HashSet<string> permissions = await _authorizationService.GetPermissionsForUserAsync(_userContext.IdentityId);
        bool isAdmin = permissions.Contains(Permission.BookingsRead.Name);

        if (!isAdmin && ownerId.Value != _userContext.UserId)
        {
            return Result.Failure<PagedResponse<BookingSummaryResponse>>(BookingErrors.Unauthorized);
        }

        // 3. Query Bookings
        var conditions = new List<string> { "apartment_id = @ApartmentId" };

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
                request.ApartmentId,
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
