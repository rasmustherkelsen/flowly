using Flowly.MessagingAbstractions;

namespace Flowly.MessageInfrastructure.Registration;

public interface IMessageBusClientRegistry
{
    string PrimaryProviderName { get; }

    IMessageBusClient GetClient(string providerName);

    bool IsRegistered(string providerName);

    IReadOnlyList<RegisteredTransport> GetAll();

    void Register(string providerName, IMessageBusClient client, bool? createTopologyOverride);
}

public record RegisteredTransport(string Name, bool IsPrimary, bool? CreateTopologyOverride);
