namespace Flowly.MessageInfrastructure.Telemetry;

internal sealed class NullCallerInstrumentation : ICallerInstrumentation
{
    public void RecordSucceeded(string callQueueName, double durationMs) { }

    public void RecordFailed(string callQueueName) { }
}
