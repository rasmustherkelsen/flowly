namespace Flowly.AzureEventHubs.Tests.Fakes;

internal sealed class FixedUtcTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
