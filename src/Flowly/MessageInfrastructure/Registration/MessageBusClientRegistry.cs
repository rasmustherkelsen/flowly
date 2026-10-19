using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

internal sealed class MessageBusClientRegistry : IMessageBusClientRegistry
{
    private readonly Dictionary<string, (ITransportClient Client, RegisteredTransport Transport)> _entries
        = new(StringComparer.OrdinalIgnoreCase);

    public string PrimaryProviderName => _entries.Values
                                             .FirstOrDefault(e => e.Transport.IsPrimary).Transport?.Name
                                         ?? throw new InvalidOperationException(DescribeMissingPrimary());

    public void Register(string providerName, IMessageBusClient client, bool? createTopologyOverride)
        => Register(providerName, (ITransportClient)client, createTopologyOverride);

    public void Register(string providerName, ITransportClient client, bool? createTopologyOverride)
    {
        if (_entries.ContainsKey(providerName))
            throw new InvalidOperationException($"A transport provider named '{providerName}' is already registered.");

        var isPrimary = client is IMessageBusClient && !_entries.Values.Any(e => e.Transport.IsPrimary);
        _entries[providerName] = (client, new RegisteredTransport(providerName, isPrimary, createTopologyOverride));
    }

    public IMessageBusClient GetClient(string providerName)
        => this.GetClient<IMessageBusClient>(providerName);

    public ITransportClient GetTransportClient(string providerName)
    {
        if (_entries.TryGetValue(providerName, out var entry))
            return entry.Client;

        var available = string.Join(", ", _entries.Keys);

        throw new InvalidOperationException(
            $"No transport provider named '{providerName}' is registered. " +
            $"Available providers: {available}");
    }

    public bool IsRegistered(string providerName)
    {
        return _entries.ContainsKey(providerName);
    }

    public IReadOnlyList<RegisteredTransport> GetAll()
    {
        return _entries.Values.Select(e => e.Transport).ToList();
    }

    private string DescribeMissingPrimary()
        => _entries.Count == 0
            ? "No transport providers have been registered. Call UseAzureServiceBus() or UseRabbitMq() before registering handlers or submitters."
            : "No registered transport provider supports queue-based messaging, so there is no primary provider. " +
              TransportClientCapability.DescribeRegistered(_entries.Values.Select(e => (e.Transport.Name, e.Client)));
}
