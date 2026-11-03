using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;

namespace Flowly.RabbitMQ.Tests.Fakes;

internal sealed class FakeEventTopologyCreatorRegistry : IEventTopologyCreatorRegistry
{
    public void Register(string providerName, IEventTopologyCreator creator)
    {
    }

    public IEventTopologyCreator? TryGetCreator(string providerName)
    {
        return null;
    }
}
