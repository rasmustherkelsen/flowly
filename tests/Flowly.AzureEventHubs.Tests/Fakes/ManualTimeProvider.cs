namespace Flowly.AzureEventHubs.Tests.Fakes;

internal sealed class ManualTimeProvider : TimeProvider
{
    private long _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _timestamp;

    public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
}
