using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;

namespace Flowly.DeadLetters.Tests.Fakes;

internal class FakeMessageBusClientRegistry(IMessageBusClient client) : IMessageBusClientRegistry
{
    private const string Primary = "__primary__";

    public string PrimaryProviderName => Primary;

    public IMessageBusClient GetClient(string providerName) => client;

    public bool IsRegistered(string providerName) => true;

    public IReadOnlyList<RegisteredTransport> GetAll() =>
        [new RegisteredTransport(Primary, IsPrimary: true, CreateTopologyOverride: null)];

    public void Register(string providerName, IMessageBusClient messageBusClient, bool? createTopologyOverride) { }
}
