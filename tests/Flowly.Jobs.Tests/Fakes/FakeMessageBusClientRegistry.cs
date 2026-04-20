using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;

namespace Flowly.Jobs.Tests.Fakes;

internal class FakeMessageBusClientRegistry(IMessageBusClient client, string primaryProviderName = "primary") : IMessageBusClientRegistry
{
    public string PrimaryProviderName { get; } = primaryProviderName;

    public IMessageBusClient GetClient(string providerName) => client;

    public bool IsRegistered(string providerName) => true;

    public IReadOnlyList<RegisteredTransport> GetAll() =>
        [new RegisteredTransport(PrimaryProviderName, IsPrimary: true, CreateTopologyOverride: null)];

    public void Register(string providerName, IMessageBusClient messageBusClient, bool? createTopologyOverride) { }
}
