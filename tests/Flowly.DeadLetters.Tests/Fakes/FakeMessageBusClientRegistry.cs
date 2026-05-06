using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;

namespace Flowly.DeadLetters.Tests.Fakes;

internal class FakeMessageBusClientRegistry(IMessageBusClient client) : IMessageBusClientRegistry
{
    public string PrimaryProviderName => "azure-service-bus";

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
        return [new RegisteredTransport("azure-service-bus", true, null)];
    }

    public void Register(string providerName, IMessageBusClient messageBusClient, bool? createTopologyOverride)
    {
    }
}