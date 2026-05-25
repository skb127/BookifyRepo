namespace Bookify.Infrastructure.Bookings;

public sealed class ExpireBookingsJobOptions
{
    public string CronExpression { get; set; } = "0 */15 * * * ?"; // Every 15 minutes by default
}
