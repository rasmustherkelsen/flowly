using Flowly.MessageInfrastructure.StreamPartitions;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.ManagementApi.Tests.Fakes;

internal sealed class FakeStreamPartitionHost(TimeProvider timeProvider)
{
    public const string HandlerName = "OrderHandler";

    public StreamPartitionStateRegistry StateRegistry { get; } = new(timeProvider);

    public StreamPartitionResumeRegistry ResumeRegistry { get; } = new();

    public FakeResumableStreamPartition Add(string queueName, string consumerName, int? partition, StreamPartitionState state, Exception? error = null)
    {
        var key = new StreamPartitionKey(queueName, consumerName, partition);
        var resumable = new FakeResumableStreamPartition(StateRegistry, HandlerName, key);

        StateRegistry.Set(HandlerName, key, state, error);

        if (state != StreamPartitionState.Waiting)
            ResumeRegistry.Register(key, resumable);

        return resumable;
    }

    public void AddTo(IServiceCollection services)
    {
        services.AddSingleton<IStreamPartitionStateReader>(StateRegistry);
        services.AddSingleton<IStreamPartitionResumer>(ResumeRegistry);
    }
}
