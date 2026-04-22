namespace Flowly;

/// <summary>
///     Interface for raising events of type TEvent to the underlying messaging system.
/// </summary>
public interface IEventSender
{
    Task RaiseEvent<TEvent>(TEvent @event, CancellationToken cancellationToken = default);
}