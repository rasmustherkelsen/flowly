using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

public interface IQueueManager
{
    void RegisterQueue(DeferredQueueRegistration registration);

    IReadOnlyList<IQueueDescription> GetRegisteredQueues();
}