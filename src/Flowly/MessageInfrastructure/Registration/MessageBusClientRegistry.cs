using Flowly.MessagingAbstractions;

namespace Flowly.MessageInfrastructure.Registration;

internal sealed class MessageBusClientRegistry : IMessageBusClientRegistry
{
    private readonly Dictionary<string, (IMessageBusClient Client, RegisteredTransport Transport)> _entries
        = new(StringComparer.OrdinalIgnoreCase);

    public string PrimaryProviderName => _entries.Values
        .FirstOrDefault(e => e.Transport.IsPrimary).Transport?.Name
        ?? throw new InvalidOperationException(
            "No transport providers have been registered. " +
            "Call UseAzureServiceBus() or UseRabbitMq() before registering handlers or submitters.");

    public void Register(string providerName, IMessageBusClient client, bool? createTopologyOverride)
    {
        if (_entries.ContainsKey(providerName))
            throw new InvalidOperationException($"A transport provider named '{providerName}' is already registered.");

        var isPrimary = _entries.Count == 0;
        _entries[providerName] = (client, new RegisteredTransport(providerName, isPrimary, createTopologyOverride));
    }

    public IMessageBusClient GetClient(string providerName)
    {
        if (_entries.TryGetValue(providerName, out var entry))
            return entry.Client;

        var available = string.Join(", ", _entries.Keys);

        throw new InvalidOperationException(
            $"No transport provider named '{providerName}' is registered. " +
            $"Available providers: {available}");
    }

    public bool IsRegistered(string providerName) => _entries.ContainsKey(providerName);

    public IReadOnlyList<RegisteredTransport> GetAll() =>
        _entries.Values.Select(e => e.Transport).ToList();
}
