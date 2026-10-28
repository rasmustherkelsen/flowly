namespace Flowly.AzureEventHubs.Tests.Emulator.Support;

internal static class Poll
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

    public static async Task Until(Func<Task<bool>> condition, string description, TimeSpan? timeout = null)
    {
        using var cancellation = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(60));
        using var timer = new PeriodicTimer(Interval);

        try
        {
            do
            {
                if (await condition())
                    return;
            }
            while (await timer.WaitForNextTickAsync(cancellation.Token));
        }
        catch (OperationCanceledException)
        {
        }

        throw new TimeoutException($"Timed out waiting for {description}.");
    }
}
