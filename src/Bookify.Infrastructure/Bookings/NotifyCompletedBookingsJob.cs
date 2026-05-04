using Bookify.Application.Bookings.NotifyCompletedBookings;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bookify.Infrastructure.Bookings;

/// <summary>
/// Quartz job that triggers the notification of completed bookings.
/// This job runs on a configurable schedule and delegates the actual work
/// to the NotifyCompletedBookingsCommandHandler
/// </summary>
[DisallowConcurrentExecution]
internal sealed class NotifyCompletedBookingsJob : IJob
{
    private readonly ISender _sender;
    private readonly NotifyCompletedBookingsJobOptions _options;
    private readonly ILogger<NotifyCompletedBookingsJob> _logger;

    public NotifyCompletedBookingsJob(
        ISender sender, 
        Microsoft.Extensions.Options.IOptions<NotifyCompletedBookingsJobOptions> options,
        ILogger<NotifyCompletedBookingsJob> logger)
    {
        _sender = sender;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Beginning to process completed bookings notification job.");

        try
        {
            var command = new NotifyCompletedBookingsCommand(_options.BatchSize);

            Domain.Abstractions.Result result = await _sender.Send(command, context.CancellationToken);

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to process completed bookings notifications. Error: {ErrorName} - {ErrorCode}",
                    result.Error.Name,
                    result.Error.Code);
            }
        }
        catch (Exception caughtException)
        {
            _logger.LogError(caughtException, "An unexpected exception occurred while processing the completed bookings notification job.");
        }

        _logger.LogInformation("Finished processing completed bookings notification job.");
    }
}
