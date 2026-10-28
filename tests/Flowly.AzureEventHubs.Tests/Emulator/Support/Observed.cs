namespace Flowly.AzureEventHubs.Tests.Emulator.Support;

internal sealed class Observed<T>
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(45);

    private readonly List<T> _items = [];
    private readonly List<(Func<IReadOnlyList<T>, bool> Condition, TaskCompletionSource Signal)> _waiters = [];
    private readonly Lock _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _items.Count;
            }
        }
    }

    public void Add(T item)
    {
        List<TaskCompletionSource> satisfied = [];

        lock (_gate)
        {
            _items.Add(item);

            for (var index = _waiters.Count - 1; index >= 0; index--)
            {
                if (_waiters[index].Condition(_items))
                {
                    satisfied.Add(_waiters[index].Signal);
                    _waiters.RemoveAt(index);
                }
            }
        }

        foreach (var signal in satisfied)
        {
            signal.TrySetResult();
        }
    }

    public IReadOnlyList<T> Snapshot()
    {
        lock (_gate)
        {
            return [.. _items];
        }
    }

    public async Task<IReadOnlyList<T>> WaitUntil(Func<IReadOnlyList<T>, bool> condition, string description, TimeSpan? timeout = null)
    {
        TaskCompletionSource signal;

        lock (_gate)
        {
            if (condition(_items))
                return [.. _items];

            signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((condition, signal));
        }

        try
        {
            await signal.Task.WaitAsync(timeout ?? DefaultTimeout);
        }
        catch (TimeoutException)
        {
            lock (_gate)
            {
                _waiters.RemoveAll(waiter => waiter.Signal == signal);
            }

            throw new TimeoutException($"Timed out waiting for {description}. Observed {Count} item(s): {Describe()}");
        }

        return Snapshot();
    }

    public Task<IReadOnlyList<T>> WaitForCount(int count, string description, TimeSpan? timeout = null)
        => WaitUntil(items => items.Count >= count, description, timeout);

    private string Describe()
    {
        var items = Snapshot();

        return string.Join("; ", items.Take(40).Select(item => item?.ToString()));
    }
}
