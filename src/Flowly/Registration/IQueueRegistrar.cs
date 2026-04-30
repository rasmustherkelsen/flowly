using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Registration;

/// <summary>
///     Registers queue descriptions into the DI service collection so that the topology creation and background services
///     can discover all queues required by the application. Transport providers and internal Flowly components call this
///     interface when they need to ensure a queue exists at startup. The registered descriptions are later collected by
///     the queue manager and passed to <see cref="IMessagingTopologyCreator.CreateTopology" />.
/// </summary>
public interface IQueueRegistrar
{
    /// <summary>
    ///     Registers a queue based on a deferred registration that was collected during handler/submitter setup. The queue
    ///     name and options are resolved from the <paramref name="registration" /> according to attribute and programmatic
    ///     configuration on the handler type.
    /// </summary>
    /// <param name="services">The service collection to register the queue description into.</param>
    /// <param name="registration">The deferred registration descriptor produced during handler/submitter setup.</param>
    /// <param name="providerName">
    ///     The name of the provider that owns this queue. Pass <see langword="null" /> to use the
    ///     primary registered provider.
    /// </param>
    void Register(IServiceCollection services, DeferredQueueRegistration registration, string? providerName = null);

    /// <summary>
    ///     Registers a queue by name with minimal configuration. Used internally to register infrastructure queues (e.g.
    ///     the recurring-job lane queue) where no handler type is available to resolve attributes from.
    /// </summary>
    /// <param name="services">The service collection to register the queue description into.</param>
    /// <param name="queueName">The name of the queue to register.</param>
    /// <param name="requiresSession">
    ///     <see langword="true" /> if the queue requires session-based (ordered, per-session)
    ///     processing; <see langword="false" /> otherwise.
    /// </param>
    /// <param name="providerName">
    ///     The name of the provider that owns this queue. Pass <see langword="null" /> to use the
    ///     primary registered provider.
    /// </param>
    void Register(IServiceCollection services, string queueName, bool requiresSession = false, string? providerName = null);
}