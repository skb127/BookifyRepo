using System.Data;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Dapper;

namespace Bookify.Application.Reviews.GetApartmentReviews;

internal sealed class GetApartmentReviewsQueryHandler
    : IQueryHandler<GetApartmentReviewsQuery, ApartmentReviewsResponse>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetApartmentReviewsQueryHandler(ISqlConnectionFactory sqlConnectionFactory) =>
        _sqlConnectionFactory = sqlConnectionFactory;

    public async Task<Result<ApartmentReviewsResponse>> Handle(
        GetApartmentReviewsQuery request,
        CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        // 1. Verify Apartment exists
        bool apartmentExists = await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1
                FROM apartments
                WHERE id = @ApartmentId AND deleted_at IS NULL
            )
            """,
            new { request.ApartmentId });

        if (!apartmentExists)
        {
            return Result.Failure<ApartmentReviewsResponse>(ApartmentErrors.NotFound);
        }

        // 2. Query Reviews
        int offset = (request.Page - 1) * request.PageSize;

        IEnumerable<ReviewSummaryResponse> dataTask = await connection.QueryAsync<ReviewSummaryResponse>(
            """
            SELECT
                id AS Id,
                user_id AS UserId,
                rating AS Rating,
                comment AS Comment,
                created_on_utc AS CreatedOnUtc
            FROM reviews
            WHERE apartment_id = @ApartmentId AND deleted_on_utc IS NULL
            ORDER BY created_on_utc DESC
            LIMIT @PageSize OFFSET @Offset
            """,
            new
            {
                request.ApartmentId,
                request.PageSize,
                Offset = offset
            });

        // 3. Query Count and Average Rating
        (int TotalCount, double? AverageRating) stats = await connection.QueryFirstOrDefaultAsync<(int TotalCount, double? AverageRating)>(
            """
            SELECT 
                COUNT(*) AS TotalCount, 
                AVG(CAST(rating AS float)) AS AverageRating
            FROM reviews 
            WHERE apartment_id = @ApartmentId AND deleted_on_utc IS NULL
            """,
            new { request.ApartmentId });

        double averageRatingRaw = stats.AverageRating ?? 0.0;
        double averageRating = Math.Round(averageRatingRaw, 1, MidpointRounding.AwayFromZero);

        return new ApartmentReviewsResponse
        {
            Items = dataTask.ToList(),
            TotalCount = stats.TotalCount,
            AverageRating = averageRating,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
