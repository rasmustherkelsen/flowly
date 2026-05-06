namespace Flowly.Transport;

/// <summary>
///     Called once at application startup (when <see cref="FlowlyOptions.CreateTopology" /> is <see langword="true" />)
///     to provision queues in the underlying message broker. Implement this interface in your transport provider and
///     register it so that Flowly can create all queues described by the registered handlers and submitters before the
///     application begins processing messages.
/// </summary>
public interface IMessagingTopologyCreator
{
    /// <summary>
    ///     Creates or updates all queues described by <paramref name="queueDescriptions" /> in the message broker. The
    ///     framework collects one <see cref="IQueueDescription" /> per registered handler and submitter, deduplicates them
    ///     by queue name, and passes the consolidated list to this method. Implementations should be idempotent — if a
    ///     queue already exists with compatible settings it should be left unchanged.
    /// </summary>
    /// <param name="queueDescriptions">The set of queue definitions to create or verify in the broker.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    Task CreateTopology(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken);
}