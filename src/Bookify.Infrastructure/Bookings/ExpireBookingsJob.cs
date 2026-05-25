using Bookify.Application.Bookings.ExpireBookingsBatch;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bookify.Infrastructure.Bookings;

[DisallowConcurrentExecution]
internal sealed class ExpireBookingsJob : IJob
{
    private readonly ISender _sender;
    private readonly ILogger<ExpireBookingsJob> _logger;

    public ExpireBookingsJob(ISender sender, ILogger<ExpireBookingsJob> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Beginning to process expire bookings job.");

        try
        {
            var command = new ExpireBookingsBatchCommand();

            Domain.Abstractions.Result result = await _sender.Send(command, context.CancellationToken);

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to expire bookings batch. Error: {ErrorName} - {ErrorCode}",
                    result.Error.Name,
                    result.Error.Code);
            }
        }
        catch (Exception caughtException)
        {
            _logger.LogError(caughtException, "An unexpected exception occurred while processing the expire bookings job.");
        }

        _logger.LogInformation("Finished processing expire bookings job.");
    }
}
