using System.Threading.RateLimiting;
using Bookify.Infrastructure.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Bookify.Infrastructure.RateLimiting;

internal sealed class SearchRateLimiterPolicy : IRateLimiterPolicy<string>
{
    private readonly PolicyOptions _options;

    public SearchRateLimiterPolicy(IOptions<RateLimitingOptions> options) =>
        _options = options.Value.Search;

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        string key = httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.GetIdentityId()
            : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetSlidingWindowLimiter(key, _ =>
            new SlidingWindowRateLimiterOptions
            {
                PermitLimit = _options.PermitLimit,
                Window = TimeSpan.FromSeconds(_options.WindowSeconds),
                SegmentsPerWindow = _options.SegmentsPerWindow,
                QueueLimit = 0
            });
    }
}
