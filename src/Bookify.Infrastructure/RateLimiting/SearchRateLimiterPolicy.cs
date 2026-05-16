using System.Threading.RateLimiting;
using Bookify.Infrastructure.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace Bookify.Infrastructure.RateLimiting;

internal sealed class SearchRateLimiterPolicy : IRateLimiterPolicy<string>
{
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        string key = httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.GetIdentityId()
            : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetSlidingWindowLimiter(key, _ =>
            new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 45,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 3,
                QueueLimit = 0
            });
    }
}
