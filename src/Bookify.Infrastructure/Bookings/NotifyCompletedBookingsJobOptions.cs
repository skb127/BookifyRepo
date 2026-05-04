namespace Bookify.Infrastructure.Bookings;

public sealed class NotifyCompletedBookingsJobOptions
{
    public string CronExpression { get; set; } = "0 0 0/6 * * ?";

    public int BatchSize { get; set; } = 50;
}
