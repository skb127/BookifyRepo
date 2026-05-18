using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Bookify.Infrastructure.RateLimiting;

internal sealed class HealthChecksRateLimiterPolicy : IRateLimiterPolicy<string>
{
    private readonly PolicyOptions _options;

    public HealthChecksRateLimiterPolicy(IOptions<RateLimitingOptions> options) =>
        _options = options.Value.HealthChecks;

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        string key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = _options.PermitLimit,
                Window = TimeSpan.FromSeconds(_options.WindowSeconds),
                QueueLimit = 0
            });
    }
}
