using Microsoft.Extensions.Options;
using Quartz;

namespace Bookify.Infrastructure.Bookings;

internal sealed class NotifyCompletedBookingsJobSetup : IConfigureOptions<QuartzOptions>
{
    private readonly NotifyCompletedBookingsJobOptions _options;

    public NotifyCompletedBookingsJobSetup(IOptions<NotifyCompletedBookingsJobOptions> options) =>
        _options = options.Value;

    public void Configure(QuartzOptions options)
    {
        const string jobName = nameof(NotifyCompletedBookingsJob);

        options
            .AddJob<NotifyCompletedBookingsJob>(configure => configure.WithIdentity(jobName))
            .AddTrigger(configure => configure
                .ForJob(jobName)
                .WithCronSchedule(_options.CronExpression));
    }
}
