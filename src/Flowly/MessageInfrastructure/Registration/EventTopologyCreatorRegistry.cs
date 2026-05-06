using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

internal sealed class EventTopologyCreatorRegistry : IEventTopologyCreatorRegistry
{
    private readonly Dictionary<string, IEventTopologyCreator> _creators
        = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string providerName, IEventTopologyCreator creator)
    {
        _creators[providerName] = creator;
    }

    public IEventTopologyCreator? TryGetCreator(string providerName)
    {
        return _creators.GetValueOrDefault(providerName);
    }
}