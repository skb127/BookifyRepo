namespace Bookify.Infrastructure.Bookings;

public sealed class CompleteBookingsJobOptions
{
    public string CronExpression { get; set; } = "0 0 0 * * ?"; // Daily at midnight by default
}
