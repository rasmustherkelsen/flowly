using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;

namespace Flowly.RabbitMQ.Tests.Fakes;

internal sealed class FakeMessagingTopologyCreatorRegistry : IMessagingTopologyCreatorRegistry
{
    public Dictionary<string, IMessagingTopologyCreator> Creators { get; } = [];

    public void Register(string providerName, IMessagingTopologyCreator creator)
    {
        Creators[providerName] = creator;
    }

    public IMessagingTopologyCreator? TryGetCreator(string providerName)
    {
        return Creators.GetValueOrDefault(providerName);
    }
}
