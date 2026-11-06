namespace Flowly.RabbitMQ.Tests.Fakes;

internal sealed class FakeStreamOffsetProbe : IStreamOffsetProbe
{
    public StreamProbeResult Result { get; set; } = StreamProbeResult.Inconclusive;

    public StreamProbeResult? TimestampResult { get; set; }

    public Exception? ExceptionToThrow { get; set; }

    public List<string> LastChunkProbes { get; } = [];

    public List<(string StreamName, DateTime Timestamp)> TimestampProbes { get; } = [];

    public Task<StreamProbeResult> ProbeLastChunk(string streamName, CancellationToken cancellationToken)
    {
        LastChunkProbes.Add(streamName);

        return Respond(Result);
    }

    public Task<StreamProbeResult> ProbeFromTimestamp(string streamName, DateTime timestamp, CancellationToken cancellationToken)
    {
        TimestampProbes.Add((streamName, timestamp));

        return Respond(TimestampResult ?? Result);
    }

    private Task<StreamProbeResult> Respond(StreamProbeResult result)
        => ExceptionToThrow is null ? Task.FromResult(result) : Task.FromException<StreamProbeResult>(ExceptionToThrow);
}
