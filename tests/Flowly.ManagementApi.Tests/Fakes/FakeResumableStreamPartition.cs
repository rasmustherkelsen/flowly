using Flowly.MessageInfrastructure.StreamPartitions;

namespace Flowly.ManagementApi.Tests.Fakes;

internal sealed class FakeResumableStreamPartition(IStreamPartitionStateRegistry stateRegistry, string handlerName, StreamPartitionKey key) : IResumableStreamPartition
{
    public int ResumeCalls { get; private set; }

    public bool Resume()
    {
        ResumeCalls++;

        if (stateRegistry.Get(key)?.State != StreamPartitionState.Halted)
            return false;

        stateRegistry.Set(handlerName, key, StreamPartitionState.Running, null);

        return true;
    }
}
