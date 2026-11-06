using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;

namespace Flowly.MessageStreams.Tests.Fakes;

internal sealed class FakeMessageBusClientRegistry : IMessageBusClientRegistry
{
    private readonly Dictionary<string, ITransportClient> _clients = new();

    public string PrimaryProviderName { get; private set; } = "primary";

    public void Register(string providerName, IMessageBusClient client, bool? createTopologyOverride)
        => Register(providerName, (ITransportClient)client, createTopologyOverride);

    public void Register(string providerName, ITransportClient client, bool? createTopologyOverride)
    {
        if (client is IMessageBusClient && !_clients.Values.Any(existing => existing is IMessageBusClient))
            PrimaryProviderName = providerName;

        _clients[providerName] = client;
    }

    public IMessageBusClient GetClient(string providerName) => (IMessageBusClient)_clients[providerName];

    public ITransportClient GetTransportClient(string providerName) => _clients[providerName];

    public bool IsRegistered(string providerName) => _clients.ContainsKey(providerName);

    public IReadOnlyList<RegisteredTransport> GetAll() =>
        _clients.Keys.Select(name => new RegisteredTransport(name, name == PrimaryProviderName, null)).ToList();
}
