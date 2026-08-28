namespace Bookify.Application.IntegrationTests.Infrastructure;

internal static class PollingHelper
{
    public static async Task<T?> WaitUntilAsync<T>(
        Func<Task<T?>> action,
        Func<T?, bool> isReady,
        TimeSpan timeout,
        TimeSpan? interval = null,
        string? failureMessage = null)
    {
        interval ??= TimeSpan.FromMilliseconds(500);
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.IsCancellationRequested)
        {
            T? result = await action().ConfigureAwait(false);
            if (isReady(result))
            {
                return result;
            }

            await Task.Delay(interval.Value, CancellationToken.None).ConfigureAwait(false);
        }

        throw new TimeoutException(failureMessage ?? $"The condition was not met within {timeout}.");
    }

    public static async Task EnsureNeverAsync<T>(
        Func<Task<T?>> action,
        Func<T?, bool> isUnexpected,
        TimeSpan duration,
        TimeSpan? interval = null,
        Func<T?, string>? failureMessage = null)
    {
        interval ??= TimeSpan.FromMilliseconds(200);
        using var cts = new CancellationTokenSource(duration);

        while (!cts.IsCancellationRequested)
        {
            T? result = await action().ConfigureAwait(false);
            if (isUnexpected(result))
            {
                string message = failureMessage?.Invoke(result) ?? $"Unexpected condition was met: {result}";
                throw new InvalidOperationException(message);
            }

            await Task.Delay(interval.Value, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
