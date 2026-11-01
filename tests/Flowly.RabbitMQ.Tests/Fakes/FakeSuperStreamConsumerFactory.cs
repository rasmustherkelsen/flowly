using System.Collections.Concurrent;
using RabbitMQ.Stream.Client;

namespace Flowly.RabbitMQ.Tests.Fakes;

internal sealed class FakeSuperStreamConsumerFactory : IRabbitMqSuperStreamConsumerFactory
{
    private readonly List<(int Count, TaskCompletionSource Created)> _waiters = [];
    private readonly Lock _lock = new();

    public ConcurrentQueue<string> Events { get; init; } = new();
    public List<Incarnation> Incarnations { get; } = [];
    public Func<int, Exception?> FailCreate { get; set; } = _ => null;

    public Task<IAsyncDisposable> Create(Func<string, long, Message, Task> onMessage, Func<string, bool, Task<IOffsetType>> onConsumerUpdate)
    {
        int number;
        lock (_lock)
        {
            number = Incarnations.Count + 1;
        }

        if (FailCreate(number) is { } failure) throw failure;

        var incarnation = new Incarnation(number, Events, onMessage, onConsumerUpdate);
        List<TaskCompletionSource> ready;
        lock (_lock)
        {
            Incarnations.Add(incarnation);
            ready = _waiters.Where(waiter => waiter.Count <= Incarnations.Count).Select(waiter => waiter.Created).ToList();
            _waiters.RemoveAll(waiter => waiter.Count <= Incarnations.Count);
        }

        Events.Enqueue($"created-{number}");

        foreach (var created in ready)
        {
            created.TrySetResult();
        }

        return Task.FromResult<IAsyncDisposable>(incarnation);
    }

    public Task WhenCreated(int count)
    {
        lock (_lock)
        {
            if (Incarnations.Count >= count) return Task.CompletedTask;

            var created = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((count, created));

            return created.Task;
        }
    }

    internal sealed class Incarnation(
        int number,
        ConcurrentQueue<string> events,
        Func<string, long, Message, Task> onMessage,
        Func<string, bool, Task<IOffsetType>> onConsumerUpdate) : IAsyncDisposable
    {
        public bool Closed { get; private set; }

        public Func<Task> BeforeClose { get; set; } = () => Task.CompletedTask;

        public Task<IOffsetType> Update(string sourceStream, bool isActive) => onConsumerUpdate(sourceStream, isActive);

        public Task Deliver(string sourceStream, long offset) => onMessage(sourceStream, offset, new Message([1]));

        public async ValueTask DisposeAsync()
        {
            await BeforeClose();
            Closed = true;
            events.Enqueue($"closed-{number}");
        }
    }
}
