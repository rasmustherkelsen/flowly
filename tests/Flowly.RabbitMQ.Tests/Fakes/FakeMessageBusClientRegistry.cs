using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;

namespace Flowly.RabbitMQ.Tests.Fakes;

internal sealed class FakeMessageBusClientRegistry : IMessageBusClientRegistry
{
    private readonly List<RegisteredTransport> _transports = [];

    public List<IMessageBusClient> Clients { get; } = [];

    public string PrimaryProviderName => _transports.First(t => t.IsPrimary).Name;

    public void Register(string providerName, IMessageBusClient client, bool? createTopologyOverride)
    {
        var isPrimary = _transports.Count == 0;
        _transports.Add(new RegisteredTransport(providerName, isPrimary, createTopologyOverride));
        Clients.Add(client);
    }

    public IMessageBusClient GetClient(string providerName)
    {
        throw new NotImplementedException();
    }

    public bool IsRegistered(string providerName)
    {
        return _transports.Any(t => string.Equals(t.Name, providerName, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<RegisteredTransport> GetAll()
    {
        return _transports;
    }
}
