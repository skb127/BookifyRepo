using Microsoft.Extensions.Options;
using Quartz;

namespace Bookify.Infrastructure.Bookings;

internal sealed class ExpireHostApprovalJobSetup : IConfigureOptions<QuartzOptions>
{
    public void Configure(QuartzOptions options)
    {
        var jobKey = new JobKey(nameof(ExpireHostApprovalJob));

        options.AddJob<ExpireHostApprovalJob>(jobBuilder => jobBuilder
            .WithIdentity(jobKey)
            .StoreDurably()
            .RequestRecovery());
    }
}
