namespace Bookify.Infrastructure.RateLimiting;

public sealed class RateLimitingOptions
{
    public GlobalLimiterOptions Global { get; init; } = new();
    public PolicyOptions WriteOperations { get; init; } = new();
    public PolicyOptions Search { get; init; } = new();
    public PolicyOptions HealthChecks { get; init; } = new();
}

public sealed class GlobalLimiterOptions
{
    public int PermitLimit { get; init; } = 80;
    public int WindowSeconds { get; init; } = 60;
    public int SegmentsPerWindow { get; init; } = 2;
}

public sealed class PolicyOptions
{
    public int PermitLimit { get; init; } = 1;
    public int WindowSeconds { get; init; } = 60;
    public int SegmentsPerWindow { get; init; } = 1;
}
