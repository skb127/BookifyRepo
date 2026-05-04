using System.Data;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Options;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;

namespace Bookify.Application.Bookings.NotifyCompletedBookings;

/// <summary>
/// Handles the notification of completed bookings that were processed by the batch completion job.
/// The batch job uses raw SQL.
/// This handler fills that gap by querying for completed bookings that have not yet been notified,
/// sending emails with a Polly retry policy, and marking them as notified upon success.
/// </summary>
internal sealed class NotifyCompletedBookingsCommandHandler : ICommandHandler<NotifyCompletedBookingsCommand>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ResiliencePipeline _retryPipeline;
    private readonly BookifyAppOptions _appOptions;
    private readonly ILogger<NotifyCompletedBookingsCommandHandler> _logger;

    public NotifyCompletedBookingsCommandHandler(
        ISqlConnectionFactory sqlConnectionFactory,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        IDateTimeProvider dateTimeProvider,
        [FromKeyedServices("email-notification-retry")]
        ResiliencePipeline retryPipeline,
        IOptions<BookifyAppOptions> appOptions,
        ILogger<NotifyCompletedBookingsCommandHandler> logger)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _dateTimeProvider = dateTimeProvider;
        _retryPipeline = retryPipeline;
        _appOptions = appOptions.Value;
        _logger = logger;
    }

    public async Task<Result> Handle(NotifyCompletedBookingsCommand request, CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();
        using IDbTransaction transaction = connection.BeginTransaction();

        // Fetch bookings that are completed but have not yet received a notification email.
        // Uses FOR UPDATE SKIP LOCKED to prevent concurrent job instances from processing the same rows.
        IReadOnlyList<BookingNotificationData> bookings = await GetBookingsToNotifyAsync(connection, transaction, request.BatchSize);

        if (bookings.Count == 0)
        {
            _logger.LogInformation("No completed bookings pending notification found.");
            return Result.Success();
        }

        _logger.LogInformation("Found {Count} completed bookings to notify.", bookings.Count);

        // Process each booking individually so that a failure on one does not block the others.
        foreach (BookingNotificationData bookingData in bookings)
        {
            await ProcessBookingNotificationAsync(bookingData, connection, transaction, cancellationToken);
        }

        // Commit all the notification timestamp updates in a single transaction.
        transaction.Commit();

        return Result.Success();
    }

    /// <summary>
    /// Attempts to send the completion notification email for a single booking.
    /// Uses the Polly retry pipeline to handle transient SMTP/network errors.
    /// Only marks the booking as notified if the email is sent successfully.
    /// </summary>
    private async Task ProcessBookingNotificationAsync(
        BookingNotificationData bookingData,
        IDbConnection connection,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            Uri homeUri = _appOptions.FrontendUrl;

            // Build the template model with the booking and user data.
            var model = new
            {
                bookingData.FirstName,
                BookingId = bookingData.BookingId.ToString(),
                HomeUrl = homeUri.AbsoluteUri
            };

            // Render the HTML email body from the Scriban template.
            string emailBody = await _emailTemplateService.GenerateEmailBodyAsync(
                "BookingCompleted.html",
                model,
                cancellationToken);

            var emailMessage = new EmailMessage(
                bookingData.Email,
                "Stay Completed",
                emailBody);

            // Execute the email send through the Polly resilience pipeline.
            await _retryPipeline.ExecuteAsync(
                async (message, ct) => await _emailService.SendAsync(message, ct),
                emailMessage,
                cancellationToken);

            // Only mark the booking as notified after a successful email send.
            await MarkAsNotifiedAsync(connection, transaction, bookingData.BookingId);

            _logger.LogInformation(
                "Successfully sent completion notification for booking {BookingId}.",
                bookingData.BookingId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send completion notification for booking {BookingId} after all retry attempts. " +
                "The notification will be retried in the next job execution.",
                bookingData.BookingId);
        }
    }

    /// <summary>
    /// Queries for completed bookings that have not yet been notified.
    /// Uses FOR UPDATE SKIP LOCKED to support concurrent job instances safely.
    /// The LIMIT clause is controlled by the configurable BatchSize option.
    /// </summary>
    private async Task<IReadOnlyList<BookingNotificationData>> GetBookingsToNotifyAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        int batchSize)
    {
        const string sql = """
                           SELECT
                               b.id AS BookingId,
                               u.first_name AS FirstName,
                               u.email AS Email
                           FROM bookings b
                           INNER JOIN users u ON u.id = b.user_id
                           WHERE b.status = @Status
                             AND b.completed_notification_sent_at IS NULL
                           ORDER BY b.completed_on_utc
                           LIMIT @BatchSize
                           FOR UPDATE OF b SKIP LOCKED
                           """;

        IEnumerable<BookingNotificationData> bookings = await connection.QueryAsync<BookingNotificationData>(
            sql,
            new
            {
                Status = (int)BookingStatus.Completed,
                BatchSize = batchSize
            },
            transaction: transaction);

        return bookings.AsList();
    }

    /// <summary>
    /// Updates the completed_notification_sent_at timestamp for a successfully notified booking.
    /// This prevents the booking from being processed again in future job executions,
    /// and also prevents the domain event handler (admin endpoint) from sending a duplicate.
    /// </summary>
    private async Task MarkAsNotifiedAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid bookingId)
    {
        const string sql = """
                           UPDATE bookings
                           SET completed_notification_sent_at = @NotifiedAt
                           WHERE id = @BookingId
                           """;

        await connection.ExecuteAsync(
            sql,
            new
            {
                NotifiedAt = _dateTimeProvider.UtcNow,
                BookingId = bookingId
            },
            transaction: transaction);
    }

    private sealed record BookingNotificationData(Guid BookingId, string FirstName, string Email);
}
