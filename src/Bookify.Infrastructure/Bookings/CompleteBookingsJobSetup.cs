#pragma warning disable S125
using Microsoft.Extensions.Options;
using Quartz;

namespace Bookify.Infrastructure.Bookings;

internal sealed class CompleteBookingsJobSetup : IConfigureOptions<QuartzOptions>
{
    private readonly CompleteBookingsJobOptions _options;

    public CompleteBookingsJobSetup(IOptions<CompleteBookingsJobOptions> options) =>
        _options = options.Value;

    public void Configure(QuartzOptions options)
    {
        const string jobName = nameof(CompleteBookingsJob);

        options
            .AddJob<CompleteBookingsJob>(configure => configure.WithIdentity(jobName).StoreDurably());
            //.AddTrigger(configure => configure
            //    .ForJob(jobName)
            //    .WithCronSchedule(_options.CronExpression));
    }
}
