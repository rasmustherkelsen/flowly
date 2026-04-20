using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;

namespace Flowly.DeadLetters.Tests.Fakes;

internal class FakeMessageBusClientRegistry(IMessageBusClient client) : IMessageBusClientRegistry
{
    public string PrimaryProviderName => "azure-service-bus";

    public IMessageBusClient GetClient(string providerName) => client;

    public bool IsRegistered(string providerName) => true;

    public IReadOnlyList<RegisteredTransport> GetAll() =>
        [new RegisteredTransport("azure-service-bus", IsPrimary: true, CreateTopologyOverride: null)];

    public void Register(string providerName, IMessageBusClient messageBusClient, bool? createTopologyOverride) { }
}
