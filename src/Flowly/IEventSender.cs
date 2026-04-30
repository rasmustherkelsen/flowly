namespace Flowly;

/// <summary>
///     Interface for raising events of type TEvent to the underlying messaging system.
/// </summary>
public interface IEventSender
{
    /// <summary>
    ///     Raises an event of type TEvent to the underlying messaging system. The event will be serialized and sent to the
    ///     appropriate topic or queue based on the event type and configuration. The method is asynchronous and can be awaited
    ///     to ensure that the event has been successfully sent before proceeding with further processing. The cancellation
    ///     token can be used to abort the operation if needed, allowing for graceful shutdowns or timeouts when sending
    ///     events.
    /// </summary>
    /// <param name="event">The event to be raised</param>
    /// <param name="cancellationToken">
    ///     Can be used to abort the operation if needed, allowing for graceful shutdowns or
    ///     timeouts when sending events
    /// </param>
    /// <typeparam name="TEvent">The type of the event to be raised</typeparam>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation</returns>
    Task RaiseEvent<TEvent>(TEvent @event, CancellationToken cancellationToken = default);
}