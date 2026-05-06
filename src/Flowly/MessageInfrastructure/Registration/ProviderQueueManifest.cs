using Flowly.MessageInfrastructure.Events.Registration;

namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     A per-provider snapshot of all queue and event registrations discovered during design-time queue discovery via
///     <see cref="FlowlyDesignTimeFactory.DiscoverQueues" />. The Flowly CLI tool reads the list of
///     <see cref="ProviderQueueManifest" /> instances from the DI container to generate broker configuration files,
///     Bicep templates, and Aspire bootstrap code without starting the full application.
/// </summary>
public sealed class ProviderQueueManifest
{
    private readonly List<DeferredQueueRegistration> _queues = [];
    private readonly List<DeferredEventRegistration> _events = [];

    /// <summary>
    ///     Initialises a new manifest for a specific provider.
    /// </summary>
    /// <param name="providerName">The unique name of the transport provider (e.g. <c>"AzureServiceBus"</c>).</param>
    /// <param name="isPrimary"><see langword="true" /> if this is the default (primary) provider.</param>
    /// <param name="transportType">A string identifying the transport type (e.g. <c>"AzureServiceBus"</c>).</param>
    public ProviderQueueManifest(string providerName, bool isPrimary, string transportType)
    {
        ProviderName = providerName;
        IsPrimary = isPrimary;
        TransportType = transportType;
    }

    /// <summary>
    ///     The unique name of the transport provider this manifest belongs to.
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    ///     <see langword="true" /> if this is the primary (default) provider.
    /// </summary>
    public bool IsPrimary { get; }

    /// <summary>
    ///     A string identifying the transport technology (e.g. <c>"AzureServiceBus"</c>, <c>"RabbitMQ"</c>).
    /// </summary>
    public string TransportType { get; }

    /// <summary>
    ///     The deduplicated list of queue registrations discovered for this provider.
    /// </summary>
    public IReadOnlyList<DeferredQueueRegistration> Queues => _queues;

    /// <summary>
    ///     The deduplicated list of event topic/subscription registrations discovered for this provider.
    /// </summary>
    public IReadOnlyList<DeferredEventRegistration> Events => _events;

    internal void Add(DeferredQueueRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(registration.QueueName))
            return;

        var existing = _queues.FindIndex(q =>
            string.Equals(q.QueueName, registration.QueueName, StringComparison.OrdinalIgnoreCase));

        if (existing < 0)
        {
            _queues.Add(registration);
            return;
        }

        _queues[existing] = Merge(_queues[existing], registration, ProviderName);
    }

    internal void AddEvent(DeferredEventRegistration registration)
    {
        var alreadyRegistered = _events.Any(e =>
            string.Equals(e.TopicName, registration.TopicName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.SubscriptionName, registration.SubscriptionName, StringComparison.OrdinalIgnoreCase));

        if (!alreadyRegistered)
            _events.Add(registration);
    }

    private static DeferredQueueRegistration Merge(DeferredQueueRegistration a, DeferredQueueRegistration b, string providerName)
    {
        return new DeferredQueueRegistration(
            a.QueueName,
            a.RequiresSession || b.RequiresSession,
            MergeSetting(a.DefaultMessageTimeToLive, b.DefaultMessageTimeToLive, a.QueueName, nameof(DeferredQueueRegistration.DefaultMessageTimeToLive), providerName),
            MergeSetting(a.DeadLetterOnMessageExpiration, b.DeadLetterOnMessageExpiration, a.QueueName, nameof(DeferredQueueRegistration.DeadLetterOnMessageExpiration), providerName),
            MergeSetting(a.LockDuration, b.LockDuration, a.QueueName, nameof(DeferredQueueRegistration.LockDuration), providerName));
    }

    private static T? MergeSetting<T>(T? a, T? b, string queueName, string settingName, string providerName) where T : struct
    {
        if (a is null) return b;
        if (b is null) return a;

        if (EqualityComparer<T>.Default.Equals(a.Value, b.Value))
            return a;

        throw new InvalidOperationException($"Conflicting queue setting '{settingName}' for queue '{queueName}' on provider '{providerName}'.");
    }
}
