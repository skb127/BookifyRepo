namespace Bookify.Application.IntegrationTests.Infrastructure;

internal static class PollingHelper
{
    public static async Task<T?> WaitUntilAsync<T>(
        Func<Task<T?>> action,
        Func<T?, bool> isReady,
        TimeSpan timeout,
        TimeSpan? interval = null)
    {
        interval ??= TimeSpan.FromMilliseconds(500);
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.IsCancellationRequested)
        {
            T? result = await action();
            if (isReady(result))
            {
                return result;
            }

            await Task.Delay(interval.Value, CancellationToken.None);
        }

        throw new TimeoutException($"The condition was not met within {timeout}.");
    }
}
