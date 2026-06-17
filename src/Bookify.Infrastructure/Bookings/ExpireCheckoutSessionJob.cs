using Bookify.Application.Bookings.ExpireCheckoutSession;
using Bookify.Domain.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bookify.Infrastructure.Bookings;

[DisallowConcurrentExecution]
internal sealed class ExpireCheckoutSessionJob : IJob
{
    private readonly ISender _sender;
    private readonly ILogger<ExpireCheckoutSessionJob> _logger;

    public ExpireCheckoutSessionJob(ISender sender, ILogger<ExpireCheckoutSessionJob> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        string? bookingIdStr = context.MergedJobDataMap.GetString("BookingId");

        if (string.IsNullOrEmpty(bookingIdStr) || !Guid.TryParse(bookingIdStr, out Guid bookingId))
        {
            _logger.LogWarning("Invalid BookingId in ExpireCheckoutSessionJob execution context.");
            return;
        }

        _logger.LogInformation("Executing ExpireCheckoutSessionJob for Booking ID {BookingId}.", bookingId);

        Result result = await _sender.Send(new ExpireCheckoutSessionCommand(bookingId), context.CancellationToken);

        if (result.IsFailure)
        {
            _logger.LogError("Failed to expire checkout session for Booking ID {BookingId}. Error: {Error}", bookingId, result.Error);
            throw new JobExecutionException($"Failed to expire checkout session for booking {bookingId}: {result.Error.Code}");
        }

        _logger.LogInformation("Successfully completed ExpireCheckoutSessionJob for Booking ID {BookingId}.", bookingId);
    }
}
