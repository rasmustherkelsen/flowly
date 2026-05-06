namespace Flowly.Transport;

/// <summary>
///     Validates queue configuration before topology creation. Implement this interface in your transport provider to
///     detect and report configuration conflicts or unsupported options early — before the broker is contacted — so that
///     operators receive a clear error message at startup instead of a cryptic broker exception. The framework calls
///     <see cref="Validate" /> for all queue descriptions belonging to the provider identified by
///     <see cref="ProviderName" /> prior to invoking <see cref="IMessagingTopologyCreator.CreateTopology" />.
/// </summary>
public interface IMessagingTopologyValidator
{
    /// <summary>
    ///     The name of the transport provider this validator is associated with (e.g. <c>"AzureServiceBus"</c>). Must
    ///     match the provider name used when registering the transport with <see cref="IFlowlyBuilder" />.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    ///     Validates the supplied queue descriptions and throws <see cref="InvalidOperationException" /> for any
    ///     configuration that is invalid or unsupported by this transport. Called by the framework once per provider
    ///     before topology creation begins.
    /// </summary>
    /// <param name="queueDescriptions">The deduplicated set of queue definitions registered for this provider.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    Task Validate(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken);
}