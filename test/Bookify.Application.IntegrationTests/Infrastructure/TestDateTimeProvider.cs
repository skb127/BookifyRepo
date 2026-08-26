using Bookify.Application.Abstractions.Clock;

namespace Bookify.Application.IntegrationTests.Infrastructure;

/// <summary>
/// A controllable <see cref="IDateTimeProvider"/> for integration tests.
/// Defaults to <see cref="DateTime.UtcNow"/> when no fixed time is set,
/// existing tests are unaffected. Call <see cref="SetUtcNow"/> to freeze time for a specific test,
/// and <see cref="Reset"/> to revert to real-time behavior.
/// </summary>
public sealed class TestDateTimeProvider : IDateTimeProvider
{
    private DateTime? _fixedUtcNow;

    public DateTime UtcNow => _fixedUtcNow ?? DateTime.UtcNow;

    /// <summary>
    /// Freezes time at the specified UTC value for all subsequent reads.
    /// </summary>
    public void SetUtcNow(DateTime utcNow)
    {
        _fixedUtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }

    /// <summary>
    /// Reverts to real-time (<see cref="DateTime.UtcNow"/>).
    /// </summary>
    public void Reset()
    {
        _fixedUtcNow = null;
    }
}
