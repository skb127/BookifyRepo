using Microsoft.Extensions.Options;
using Quartz;

namespace Bookify.Infrastructure.Bookings;

internal sealed class ExpireBookingsJobSetup : IConfigureOptions<QuartzOptions>
{
    private readonly ExpireBookingsJobOptions _options;

    public ExpireBookingsJobSetup(IOptions<ExpireBookingsJobOptions> options) =>
        _options = options.Value;

    public void Configure(QuartzOptions options)
    {
        const string jobName = nameof(ExpireBookingsJob);

        options
            .AddJob<ExpireBookingsJob>(configure => configure.WithIdentity(jobName))
            .AddTrigger(configure => configure
                .ForJob(jobName)
                .WithCronSchedule(_options.CronExpression));
    }
}
