namespace Flowly.DeadLetters.Tests.Telemetry;

[CollectionDefinition(Name, DisableParallelization = true)]
public class TelemetryTestCollection
{
    public const string Name = "Flowly.DeadLetters telemetry (MeterListener/ActivityListener)";
}
