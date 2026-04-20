using Flowly.MessagingAbstractions;

namespace Flowly.MessageInfrastructure.Registration;

internal sealed class EventTopologyCreatorRegistry : IEventTopologyCreatorRegistry
{
    private readonly Dictionary<string, IEventTopologyCreator> _creators
        = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string providerName, IEventTopologyCreator creator) =>
        _creators[providerName] = creator;

    public IEventTopologyCreator? TryGetCreator(string providerName) =>
        _creators.GetValueOrDefault(providerName);
}
