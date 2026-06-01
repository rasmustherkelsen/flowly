namespace Flowly.Transport;

/// <summary>
///     Marker interface for reply queue descriptions. Transport topology creators check for this interface to skip
///     DLX, retry queue, and dead-letter infrastructure — reply queues are simple durable queues consumed in
///     receive-and-delete mode.
/// </summary>
public interface IReplyQueueDescription : IQueueDescription
{
}
