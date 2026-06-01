using System.Collections.Concurrent;

namespace Flowly.MessageInfrastructure.Callers;

internal class PendingCallRegistry<TReturn> : IPendingCallRegistry<TReturn> where TReturn : class
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<TReturn>> _pending = new();

    public Task<TReturn> Register(string correlationId, CancellationToken cancellationToken)
    {
        var taskCompletionSource = new TaskCompletionSource<TReturn>(TaskCreationOptions.RunContinuationsAsynchronously);

        _pending[correlationId] = taskCompletionSource;

        cancellationToken.Register(() =>
        {
            if (_pending.TryRemove(correlationId, out var removed))
                removed.TrySetCanceled(cancellationToken);
        });

        return taskCompletionSource.Task;
    }

    public bool TryResolve(string correlationId, TReturn response)
    {
        if (!_pending.TryRemove(correlationId, out var taskCompletionSource))
            return false;

        taskCompletionSource.TrySetResult(response);
        return true;
    }
}
