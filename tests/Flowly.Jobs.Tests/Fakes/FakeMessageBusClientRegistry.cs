using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;

namespace Flowly.Jobs.Tests.Fakes;

internal class FakeMessageBusClientRegistry(IMessageBusClient client, string primaryProviderName = "primary") : IMessageBusClientRegistry
{
    public string PrimaryProviderName { get; } = primaryProviderName;

    public IMessageBusClient GetClient(string providerName)
    {
        return client;
    }

    public bool IsRegistered(string providerName)
    {
        return true;
    }

    public IReadOnlyList<RegisteredTransport> GetAll()
    {
        return [new RegisteredTransport(PrimaryProviderName, true, null)];
    }

    public void Register(string providerName, IMessageBusClient messageBusClient, bool? createTopologyOverride)
    {
    }
}