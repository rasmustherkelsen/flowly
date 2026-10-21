using Flowly.Transport;

namespace Flowly.Tests.Fakes;

internal sealed class FakeStreamStartOffsetResolver(List<string>? events = null) : IStreamStartOffsetResolver
{
    public long? Result { get; set; }

    public Exception? ExceptionToThrow { get; set; }

    public List<ResolveCall> Calls { get; } = [];

    public Task<long?> ResolveStartOffset(string queueName, int? partition, StartPosition startPosition, CancellationToken cancellationToken)
    {
        events?.Add("Resolve");
        Calls.Add(new ResolveCall(queueName, partition, startPosition));

        return ExceptionToThrow is null ? Task.FromResult(Result) : Task.FromException<long?>(ExceptionToThrow);
    }

    internal sealed record ResolveCall(string QueueName, int? Partition, StartPosition StartPosition);
}
