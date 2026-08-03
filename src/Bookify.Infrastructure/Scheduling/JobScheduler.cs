using Bookify.Application.Abstractions.Scheduling;
using Bookify.Infrastructure.Bookings;
using Quartz;

namespace Bookify.Infrastructure.Scheduling;

internal sealed class JobScheduler : IJobScheduler
{
    private readonly ISchedulerFactory _schedulerFactory;

    public JobScheduler(ISchedulerFactory schedulerFactory) => _schedulerFactory = schedulerFactory;

    public async Task ScheduleExpireCheckoutSessionAsync(
        Guid bookingId,
        DateTime fireAt,
        CancellationToken cancellationToken = default)
    {
        IScheduler scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        var jobKey = new JobKey(nameof(ExpireCheckoutSessionJob));
        var triggerKey = new TriggerKey($"expire-checkout-{bookingId}");

        ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .UsingJobData("BookingId", bookingId.ToString())
            .StartAt(new DateTimeOffset(DateTime.SpecifyKind(fireAt, DateTimeKind.Utc)))
            .Build();

        await scheduler.ScheduleJob(trigger, cancellationToken);
    }

    public async Task ScheduleExpireHostApprovalAsync(
        Guid bookingId,
        DateTime fireAt,
        CancellationToken cancellationToken = default)
    {
        IScheduler scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        var jobKey = new JobKey(nameof(ExpireHostApprovalJob));
        var triggerKey = new TriggerKey($"expire-host-approval-{bookingId}");

        ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .UsingJobData("BookingId", bookingId.ToString())
            .StartAt(new DateTimeOffset(DateTime.SpecifyKind(fireAt, DateTimeKind.Utc)))
            .Build();

        await scheduler.ScheduleJob(trigger, cancellationToken);
    }

    public async Task CancelExpireCheckoutSessionAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        IScheduler scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        var triggerKey = new TriggerKey($"expire-checkout-{bookingId}");

        await scheduler.UnscheduleJob(triggerKey, cancellationToken);
    }

    public async Task CancelExpireHostApprovalAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        IScheduler scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        var triggerKey = new TriggerKey($"expire-host-approval-{bookingId}");

        await scheduler.UnscheduleJob(triggerKey, cancellationToken);
    }

    public async Task ScheduleAccountDeletionAsync(
        Guid userId,
        DateTime fireAt,
        CancellationToken cancellationToken = default)
    {
        IScheduler scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        var jobKey = new JobKey(nameof(Users.FinalizeAccountDeletionJob));
        var triggerKey = new TriggerKey($"finalize-account-deletion-{userId}");

        ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .UsingJobData("UserId", userId.ToString())
            .StartAt(new DateTimeOffset(DateTime.SpecifyKind(fireAt, DateTimeKind.Utc)))
            .Build();

        await scheduler.ScheduleJob(trigger, cancellationToken);
    }

    public async Task CancelAccountDeletionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        IScheduler scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        var triggerKey = new TriggerKey($"finalize-account-deletion-{userId}");

        await scheduler.UnscheduleJob(triggerKey, cancellationToken);
    }
}
