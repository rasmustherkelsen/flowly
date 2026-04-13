using Flowly.MessagingAbstractions;

namespace Flowly.MessageInfrastructure.Registration;

public interface IMessagingTopologyCreatorRegistry
{
    void Register(string providerName, IMessagingTopologyCreator creator);

    IMessagingTopologyCreator GetCreator(string providerName);
}
