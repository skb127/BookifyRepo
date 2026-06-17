using Microsoft.Extensions.Options;
using Quartz;

namespace Bookify.Infrastructure.Bookings;

internal sealed class ExpireCheckoutSessionJobSetup : IConfigureOptions<QuartzOptions>
{
    public void Configure(QuartzOptions options)
    {
        var jobKey = new JobKey(nameof(ExpireCheckoutSessionJob));

        options.AddJob<ExpireCheckoutSessionJob>(jobBuilder => jobBuilder
            .WithIdentity(jobKey)
            .StoreDurably()
            .RequestRecovery());
    }
}
