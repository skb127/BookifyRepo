using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Reviews;
using Dapper;

namespace Bookify.Application.Reviews.GetReview;

internal sealed class GetReviewQueryHandler : IQueryHandler<GetReviewQuery, ReviewResponse>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetReviewQueryHandler(ISqlConnectionFactory sqlConnectionFactory) =>
        _sqlConnectionFactory = sqlConnectionFactory;

    public async Task<Result<ReviewResponse>> Handle(GetReviewQuery request, CancellationToken cancellationToken)
    {
        using System.Data.IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
            SELECT
                id AS Id,
                booking_id AS BookingId,
                apartment_id AS ApartmentId,
                user_id AS UserId,
                rating AS Rating,
                comment AS Comment,
                created_on_utc AS CreatedOnUtc
            FROM reviews
            WHERE id = @ReviewId AND deleted_on_utc IS NULL
            """;

        ReviewResponse? review = await connection.QueryFirstOrDefaultAsync<ReviewResponse>(
            sql,
            new
            {
                request.ReviewId
            });

        return review ?? Result.Failure<ReviewResponse>(ReviewErrors.NotFound);
    }
}
