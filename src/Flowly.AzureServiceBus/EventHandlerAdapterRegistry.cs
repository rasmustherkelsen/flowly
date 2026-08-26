namespace Flowly.AzureServiceBus;

/// <summary>
///     Shared, thread-safe bookkeeping for the three ASB processor wrappers (<see cref="MessageBusProcessor{TMessage}" />,
///     <see cref="AzureServiceBusEventProcessor{TEvent}" />, <see cref="ExecutionLaneProcessor" />) that expose a
///     Flowly-shaped delegate event while internally subscribing to an Azure SDK event whose argument type differs.
///     Each call to <see cref="Add" /> wraps the caller's delegate with <c>adapterFactory</c> and remembers the pairing
///     so a later <see cref="Remove" /> can locate and unsubscribe the matching SDK-side adapter. Reproduces standard
///     multicast-delegate semantics: subscribing the same delegate instance more than once is supported (each
///     subscription gets its own adapter and is invoked once per registration), and removing it once removes exactly
///     one of those subscriptions, mirroring <see cref="Delegate.Combine(Delegate,Delegate)" />/
///     <see cref="Delegate.Remove" />.
/// </summary>
/// <typeparam name="TUserDelegate">The Flowly-facing delegate type exposed on the public event.</typeparam>
/// <typeparam name="TSdkArgs">The Azure SDK event's argument type being adapted to.</typeparam>
internal sealed class EventHandlerAdapterRegistry<TUserDelegate, TSdkArgs>(Func<TUserDelegate, Func<TSdkArgs, Task>> adapterFactory)
    where TUserDelegate : Delegate
{
    private readonly Dictionary<TUserDelegate, List<Func<TSdkArgs, Task>>> _adaptersByHandler = new();
    private readonly Lock _lock = new();

    /// <summary>
    ///     Wraps <paramref name="handler" /> with the registry's adapter factory, records the pairing, and returns the
    ///     resulting SDK-side adapter for the caller to subscribe to the underlying Azure SDK event.
    /// </summary>
    /// <param name="handler">The Flowly-facing delegate being subscribed.</param>
    /// <returns>The SDK-side adapter delegate to add to the wrapped Azure SDK event.</returns>
    public Func<TSdkArgs, Task> Add(TUserDelegate handler)
    {
        var adapter = adapterFactory(handler);

        lock (_lock)
        {
            if (!_adaptersByHandler.TryGetValue(handler, out var adapters))
            {
                adapters = [];
                _adaptersByHandler[handler] = adapters;
            }

            adapters.Add(adapter);
        }

        return adapter;
    }

    /// <summary>
    ///     Removes one recorded subscription for <paramref name="handler" />, if any, and returns its SDK-side adapter
    ///     for the caller to unsubscribe from the underlying Azure SDK event. If <paramref name="handler" /> was
    ///     subscribed more than once, only one subscription is removed. If it was never subscribed, this is a no-op.
    /// </summary>
    /// <param name="handler">The Flowly-facing delegate being unsubscribed.</param>
    /// <returns>The SDK-side adapter to remove from the wrapped Azure SDK event, or <c>null</c> if none was found.</returns>
    public Func<TSdkArgs, Task>? Remove(TUserDelegate handler)
    {
        lock (_lock)
        {
            if (!_adaptersByHandler.TryGetValue(handler, out var adapters) || adapters.Count == 0) return null;

            var adapter = adapters[^1];
            adapters.RemoveAt(adapters.Count - 1);

            if (adapters.Count == 0) _adaptersByHandler.Remove(handler);

            return adapter;
        }
    }
}
