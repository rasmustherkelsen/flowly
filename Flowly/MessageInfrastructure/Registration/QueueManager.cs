using System.Collections.Concurrent;

namespace Flowly.MessageInfrastructure.Registration;

internal class QueueManager : IQueueManager
{
    private readonly ConcurrentDictionary<string, byte> _queues = new();

    public void RegisterQueue(string queueName)
    {
        if (string.IsNullOrWhiteSpace(queueName)) return;
        _queues.TryAdd(queueName, 0);
    }

    public IReadOnlyList<string> GetRegisteredQueues() => _queues.Keys.OrderBy(x => x).ToList();
}