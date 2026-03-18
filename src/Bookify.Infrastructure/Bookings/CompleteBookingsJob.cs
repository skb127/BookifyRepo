using Bookify.Application.Bookings.CompleteBookingsBatch;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bookify.Infrastructure.Bookings;

[DisallowConcurrentExecution]
internal sealed class CompleteBookingsJob : IJob
{
    private readonly ISender _sender;
    private readonly ILogger<CompleteBookingsJob> _logger;

    public CompleteBookingsJob(ISender sender, ILogger<CompleteBookingsJob> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Beginning to process complete bookings job.");
        
        try
        {
            var command = new CompleteBookingsBatchCommand();
            
            Domain.Abstractions.Result result = await _sender.Send(command, context.CancellationToken);

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to complete bookings batch. Error: {ErrorName} - {ErrorCode}",
                    result.Error.Name,
                    result.Error.Code);
            }
        }
        catch (Exception caughtException)
        {
            _logger.LogError(caughtException, "An unexpected exception occurred while processing the complete bookings job.");
        }

        _logger.LogInformation("Finished processing complete bookings job.");
    }
}
