using Microsoft.Extensions.Options;
using Quartz;

namespace Bookify.Infrastructure.Users;

internal sealed class FinalizeAccountDeletionJobSetup : IConfigureOptions<QuartzOptions>
{
    public void Configure(QuartzOptions options)
    {
        var jobKey = new JobKey(nameof(FinalizeAccountDeletionJob));

        options.AddJob<FinalizeAccountDeletionJob>(jobBuilder => jobBuilder
            .WithIdentity(jobKey)
            .StoreDurably()
            .RequestRecovery());
    }
}
