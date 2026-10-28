using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

internal sealed class MessagingTopologyCreatorRegistry : IMessagingTopologyCreatorRegistry
{
    private readonly Dictionary<string, IMessagingTopologyCreator> _creators
        = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string providerName, IMessagingTopologyCreator creator)
    {
        _creators[providerName] = creator;
    }

    public IMessagingTopologyCreator? TryGetCreator(string providerName)
        => _creators.GetValueOrDefault(providerName);

    public IMessagingTopologyCreator GetCreator(string providerName)
    {
        if (_creators.TryGetValue(providerName, out var creator))
            return creator;

        throw new InvalidOperationException(
            $"No topology creator registered for provider '{providerName}'.");
    }
}