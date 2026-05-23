using System.Data;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Dapper;
using Microsoft.Extensions.Logging;

namespace Bookify.Application.Bookings.CompleteBookingsBatch;

internal sealed class CompleteBookingsBatchCommandHandler : ICommandHandler<CompleteBookingsBatchCommand>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CompleteBookingsBatchCommandHandler> _logger;

    public CompleteBookingsBatchCommandHandler(
        ISqlConnectionFactory sqlConnectionFactory,
        IDateTimeProvider dateTimeProvider,
        ILogger<CompleteBookingsBatchCommandHandler> logger)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result> Handle(CompleteBookingsBatchCommand request, CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();
        using IDbTransaction transaction = connection.BeginTransaction();

        IReadOnlyList<Guid> bookingIds = await GetBookingsToCompleteAsync(connection, transaction);

        if (bookingIds.Count == 0)
        {
            _logger.LogInformation("No in-progress bookings ready to be completed found.");
            return Result.Success();
        }

        _logger.LogInformation("Found {Count} bookings to complete.", bookingIds.Count);

        const string sql = """
                           UPDATE bookings
                           SET status = @Status,
                               completed_on_utc = @CompletedOnUtc
                           WHERE id = ANY(@BookingIds)
                           """;

        await connection.ExecuteAsync(
            sql,
            new
            {
                Status = (int)BookingStatus.Completed,
                CompletedOnUtc = _dateTimeProvider.UtcNow,
                BookingIds = bookingIds
            },
            transaction: transaction);

        transaction.Commit();

        return Result.Success();
    }

    private async Task<IReadOnlyList<Guid>> GetBookingsToCompleteAsync(
        IDbConnection connection,
        IDbTransaction transaction)
    {
        // The `FOR UPDATE` clause locks the selected rows to prevent other transactions
        // from modifying them until the current transaction is committed.
        // This is crucial to avoid race conditions where two job instances might process the same bookings.
        // `SKIP LOCKED` is added to allow concurrent jobs to work on different sets of rows
        // instead of waiting for the lock, which is more efficient for this background job.
        const string sql = """
                           SELECT id
                           FROM bookings
                           WHERE status = @Status
                             AND duration_end < @TodayDate
                           FOR UPDATE SKIP LOCKED
                           """;

        IEnumerable<Guid> bookingIds = await connection.QueryAsync<Guid>(
            sql,
            new
            {
                Status = (int)BookingStatus.InProgress,
                TodayDate = DateOnly.FromDateTime(_dateTimeProvider.UtcNow)
            },
            transaction: transaction);

        return bookingIds.AsList();
    }
}
