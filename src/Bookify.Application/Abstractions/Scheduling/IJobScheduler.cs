namespace Bookify.Application.Abstractions.Scheduling;

public interface IJobScheduler
{
    Task ScheduleExpireCheckoutSessionAsync(Guid bookingId, DateTime fireAt, CancellationToken cancellationToken = default);

    Task ScheduleExpireHostApprovalAsync(Guid bookingId, DateTime fireAt, CancellationToken cancellationToken = default);

    Task CancelExpireCheckoutSessionAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task CancelExpireHostApprovalAsync(Guid bookingId, CancellationToken cancellationToken = default);
}
