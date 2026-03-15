using System.Data;
using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Common;
using Bookify.Domain.Abstractions;
using Dapper;

namespace Bookify.Application.Reviews.GetMyReviews;

internal sealed class GetMyReviewsQueryHandler
    : IQueryHandler<GetMyReviewsQuery, PagedResponse<MyReviewResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly IUserContext _userContext;

    public GetMyReviewsQueryHandler(
        ISqlConnectionFactory sqlConnectionFactory,
        IUserContext userContext)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
        _userContext = userContext;
    }

    public async Task<Result<PagedResponse<MyReviewResponse>>> Handle(
        GetMyReviewsQuery request,
        CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        int offset = (request.Page - 1) * request.PageSize;

        IEnumerable<MyReviewResponse> dataTask = await connection.QueryAsync<MyReviewResponse>(
            """
            SELECT
                id AS Id,
                booking_id AS BookingId,
                apartment_id AS ApartmentId,
                rating AS Rating,
                comment AS Comment,
                created_on_utc AS CreatedOnUtc
            FROM reviews
            WHERE user_id = @UserId AND deleted_on_utc IS NULL
            ORDER BY created_on_utc DESC
            LIMIT @PageSize OFFSET @Offset
            """,
            new
            {
                _userContext.UserId,
                request.PageSize,
                Offset = offset
            });

        int totalCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM reviews 
            WHERE user_id = @UserId AND deleted_on_utc IS NULL
            """,
            new { _userContext.UserId });

        return new PagedResponse<MyReviewResponse>
        {
            Items = [.. dataTask],
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
