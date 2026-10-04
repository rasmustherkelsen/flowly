using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;

namespace Flowly.MessageStreams.Tests.Fakes;

internal sealed class FakeMessageBusClientRegistry : IMessageBusClientRegistry
{
    private readonly Dictionary<string, IMessageBusClient> _clients = new();

    public string PrimaryProviderName { get; private set; } = "primary";

    public void Register(string providerName, IMessageBusClient client, bool? createTopologyOverride)
    {
        if (_clients.Count == 0)
            PrimaryProviderName = providerName;

        _clients[providerName] = client;
    }

    public IMessageBusClient GetClient(string providerName) => _clients[providerName];

    public bool IsRegistered(string providerName) => _clients.ContainsKey(providerName);

    public IReadOnlyList<RegisteredTransport> GetAll() =>
        _clients.Keys.Select(name => new RegisteredTransport(name, name == PrimaryProviderName, null)).ToList();
}
