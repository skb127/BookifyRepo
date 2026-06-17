using Bookify.Application.Bookings.ExpireHostApproval;
using Bookify.Domain.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bookify.Infrastructure.Bookings;

[DisallowConcurrentExecution]
internal sealed class ExpireHostApprovalJob : IJob
{
    private readonly ISender _sender;
    private readonly ILogger<ExpireHostApprovalJob> _logger;

    public ExpireHostApprovalJob(ISender sender, ILogger<ExpireHostApprovalJob> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        string? bookingIdStr = context.MergedJobDataMap.GetString("BookingId");

        if (string.IsNullOrEmpty(bookingIdStr) || !Guid.TryParse(bookingIdStr, out Guid bookingId))
        {
            _logger.LogWarning("Invalid BookingId in ExpireHostApprovalJob execution context.");
            return;
        }

        _logger.LogInformation("Executing ExpireHostApprovalJob for Booking ID {BookingId}.", bookingId);

        Result result = await _sender.Send(new ExpireHostApprovalCommand(bookingId), context.CancellationToken);

        if (result.IsFailure)
        {
            _logger.LogError("Failed to expire host approval for Booking ID {BookingId}. Error: {Error}", bookingId, result.Error);
            throw new JobExecutionException($"Failed to expire host approval for booking {bookingId}: {result.Error.Code}");
        }

        _logger.LogInformation("Successfully completed ExpireHostApprovalJob for Booking ID {BookingId}.", bookingId);
    }
}
