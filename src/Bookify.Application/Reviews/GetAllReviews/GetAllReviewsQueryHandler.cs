using System.Data;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Common;
using Bookify.Domain.Abstractions;
using Dapper;

namespace Bookify.Application.Reviews.GetAllReviews;

internal sealed class GetAllReviewsQueryHandler : IQueryHandler<GetAllReviewsQuery, PagedResponse<AllReviewsResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetAllReviewsQueryHandler(ISqlConnectionFactory sqlConnectionFactory) =>
        _sqlConnectionFactory = sqlConnectionFactory;

    public async Task<Result<PagedResponse<AllReviewsResponse>>> Handle(
        GetAllReviewsQuery request,
        CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        var conditions = new List<string> { "deleted_on_utc IS NULL" };
        var parameters = new DynamicParameters();

        if (request.ApartmentId.HasValue)
        {
            conditions.Add("apartment_id = @ApartmentId");
            parameters.Add("@ApartmentId", request.ApartmentId.Value);
        }

        if (request.UserId.HasValue)
        {
            conditions.Add("user_id = @UserId");
            parameters.Add("@UserId", request.UserId.Value);
        }

        if (request.MinRating.HasValue)
        {
            conditions.Add("rating >= @MinRating");
            parameters.Add("@MinRating", request.MinRating.Value);
        }

        if (request.MaxRating.HasValue)
        {
            conditions.Add("rating <= @MaxRating");
            parameters.Add("@MaxRating", request.MaxRating.Value);
        }

        if (request.IsEdited.HasValue)
        {
            conditions.Add(request.IsEdited.Value ? "edited_on_utc IS NOT NULL" : "edited_on_utc IS NULL");
        }

        string whereClause = string.Join(" AND ", conditions);

        // 1. Get Total Count
        string countSql = $"""
            SELECT COUNT(*)
            FROM reviews
            WHERE {whereClause}
            """;

        int totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

        // 2. Get Data
        int offset = (request.Page - 1) * request.PageSize;
        parameters.Add("@PageSize", request.PageSize);
        parameters.Add("@Offset", offset);

        string dataSql = $"""
            SELECT
                id AS Id,
                booking_id AS BookingId,
                apartment_id AS ApartmentId,
                user_id AS UserId,
                rating AS Rating,
                comment AS Comment,
                created_on_utc AS CreatedOnUtc,
                edited_on_utc AS EditedOnUtc
            FROM reviews
            WHERE {whereClause}
            ORDER BY created_on_utc DESC
            LIMIT @PageSize OFFSET @Offset
            """;

        IEnumerable<AllReviewsResponse> dataTask = await connection.QueryAsync<AllReviewsResponse>(dataSql, parameters);

        return new PagedResponse<AllReviewsResponse>
        {
            Items = [.. dataTask],
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
