namespace Flowly.AzureEventHubs;

internal sealed class EventHubsErrorThrottle(TimeProvider timeProvider, TimeSpan interval)
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(30);

    private readonly Lock _gate = new();
    private long? _lastReported;
    private int _suppressed;

    public bool TryReport(out int suppressedSinceLastReport)
    {
        lock (_gate)
        {
            var now = timeProvider.GetTimestamp();

            if (_lastReported is { } lastReported && timeProvider.GetElapsedTime(lastReported, now) < interval)
            {
                _suppressed++;
                suppressedSinceLastReport = 0;

                return false;
            }

            _lastReported = now;
            suppressedSinceLastReport = _suppressed;
            _suppressed = 0;

            return true;
        }
    }
}
