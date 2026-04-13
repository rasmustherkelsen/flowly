namespace Flowly.MessageInfrastructure.Receivers;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true)]
public sealed class ProviderAffinityAttribute(string providerName) : Attribute
{
    public string ProviderName { get; } = providerName;
}
