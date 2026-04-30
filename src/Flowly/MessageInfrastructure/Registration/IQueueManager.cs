using Flowly.Registration;
using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Collects all deferred queue registrations produced during handler and submitter setup and exposes them as fully
///     resolved <see cref="IQueueDescription" /> instances for topology creation. The queue manager is used by
///     <see cref="IQueueRegistrar" /> to accumulate registrations and by the topology creation hosted service to
///     retrieve the final, deduplicated list of queues to provision.
/// </summary>
public interface IQueueManager
{
    /// <summary>
    ///     Adds a deferred queue registration to the manager. Duplicate registrations for the same queue name are merged;
    ///     conflicting settings throw <see cref="InvalidOperationException" />.
    /// </summary>
    /// <param name="registration">The deferred queue registration to add.</param>
    void RegisterQueue(DeferredQueueRegistration registration);

    /// <summary>
    ///     Returns the deduplicated and fully resolved set of queue descriptions that should be provisioned in the
    ///     message broker.
    /// </summary>
    /// <returns>A read-only list of <see cref="IQueueDescription" /> instances.</returns>
    IReadOnlyList<IQueueDescription> GetRegisteredQueues();
}