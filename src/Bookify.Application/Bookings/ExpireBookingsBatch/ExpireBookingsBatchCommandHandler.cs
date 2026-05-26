using System.Data;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Dapper;
using Microsoft.Extensions.Logging;
using Bookify.Application.Abstractions.Serialization;

namespace Bookify.Application.Bookings.ExpireBookingsBatch;

internal sealed class ExpireBookingsBatchCommandHandler : ICommandHandler<ExpireBookingsBatchCommand>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ExpireBookingsBatchCommandHandler> _logger;

    public ExpireBookingsBatchCommandHandler(
        ISqlConnectionFactory sqlConnectionFactory,
        IDateTimeProvider dateTimeProvider,
        ILogger<ExpireBookingsBatchCommandHandler> logger)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result> Handle(ExpireBookingsBatchCommand request, CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();
        using IDbTransaction transaction = connection.BeginTransaction();

        IReadOnlyList<Guid> bookingIds = await GetBookingsToExpireAsync(connection, transaction);

        if (bookingIds.Count == 0)
        {
            _logger.LogInformation("No reserved bookings ready to be expired found.");
            return Result.Success();
        }

        _logger.LogInformation("Found {Count} bookings to expire.", bookingIds.Count);

        const string sql = """
                           UPDATE bookings
                           SET status = @Status,
                               expired_on_utc = @ExpiredOnUtc
                           WHERE id = ANY(@BookingIds)
                           """;

        await connection.ExecuteAsync(
            sql,
            new
            {
                Status = (int)BookingStatus.Expired,
                ExpiredOnUtc = _dateTimeProvider.UtcNow,
                BookingIds = bookingIds
            },
            transaction: transaction);

        var outboxMessages = bookingIds.Select(id => new
        {
            Id = Guid.CreateVersion7(),
            OccurredOnUtc = _dateTimeProvider.UtcNow,
            Type = nameof(BookingExpiredDomainEvent),
            Content = DomainEventSerializer.Serialize(new BookingExpiredDomainEvent(id))
        }).ToList();

        const string insertOutboxSql = """
            INSERT INTO outbox_messages (id, occurred_on_utc, type, content)
            VALUES (@Id, @OccurredOnUtc, @Type, @Content::jsonb)
            """;

        await connection.ExecuteAsync(insertOutboxSql, outboxMessages, transaction: transaction);

        transaction.Commit();

        return Result.Success();
    }

    private async Task<IReadOnlyList<Guid>> GetBookingsToExpireAsync(
        IDbConnection connection,
        IDbTransaction transaction)
    {
        const string sql = """
                           SELECT id
                           FROM bookings
                           WHERE status = @Status
                             AND expires_at < @UtcNow
                           FOR UPDATE SKIP LOCKED
                           """;

        IEnumerable<Guid> bookingIds = await connection.QueryAsync<Guid>(
            sql,
            new
            {
                Status = (int)BookingStatus.Reserved,
                _dateTimeProvider.UtcNow
            },
            transaction: transaction);

        return bookingIds.AsList();
    }
}
