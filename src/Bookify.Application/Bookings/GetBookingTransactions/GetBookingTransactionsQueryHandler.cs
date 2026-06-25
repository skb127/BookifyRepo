using System.Data;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Dapper;

namespace Bookify.Application.Bookings.GetBookingTransactions;

internal sealed class GetBookingTransactionsQueryHandler : IQueryHandler<GetBookingTransactionsQuery, IReadOnlyList<BookingTransactionResponse>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetBookingTransactionsQueryHandler(ISqlConnectionFactory sqlConnectionFactory) =>
        _sqlConnectionFactory = sqlConnectionFactory;

    public async Task<Result<IReadOnlyList<BookingTransactionResponse>>> Handle(
        GetBookingTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        const string bookingExistsSql = "SELECT EXISTS(SELECT 1 FROM bookings WHERE id = @BookingId)";
        bool bookingExists = await connection.ExecuteScalarAsync<bool>(
            bookingExistsSql,
            new { request.BookingId });

        if (!bookingExists)
        {
            return Result.Failure<IReadOnlyList<BookingTransactionResponse>>(BookingErrors.NotFound);
        }

        const string sql = """
            SELECT
                id AS Id,
                booking_id AS BookingId,
                stripe_session_id AS StripeSessionId,
                stripe_payment_intent_id AS StripePaymentIntentId,
                checkout_session_url AS CheckoutSessionUrl,
                amount_amount AS Amount,
                amount_currency AS Currency,
                provider_status AS ProviderStatus,
                created_on_utc AS CreatedOnUtc,
                updated_on_utc AS UpdatedOnUtc
            FROM transactions
            WHERE booking_id = @BookingId
            ORDER BY created_on_utc DESC
            """;

        IEnumerable<BookingTransactionResponse> transactions = await connection.QueryAsync<BookingTransactionResponse>(
            sql,
            new { request.BookingId });

        IReadOnlyList<BookingTransactionResponse> list = new List<BookingTransactionResponse>(transactions).AsReadOnly();
        return Result.Success(list);
    }
}
