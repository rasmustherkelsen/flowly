using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

public interface IEventTopologyCreatorRegistry
{
    void Register(string providerName, IEventTopologyCreator creator);

    IEventTopologyCreator? TryGetCreator(string providerName);
}