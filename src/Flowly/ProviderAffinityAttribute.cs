namespace Flowly;

/// <summary>
///     Attribute to specify the provider affinity for a message contract. This is used to determine which provider should
///     be used for a given message contract when multiple providers are registered. The provider affinity is specified by
///     the provider name, which should match the name of a registered provider in the Flowly framework. By applying
///     this attribute to a message class, you can ensure that the handler processing the message is associated with the
///     correct provider, allowing for proper routing of messages to the appropriate message broker or service. This is
///     particularly useful in scenarios where multiple providers are used within the same application, enabling
///     fine-grained control over which handlers are associated with which providers based on their specified affinities.
/// </summary>
/// <param name="providerName">The name of the provider</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ProviderAffinityAttribute(string providerName) : Attribute
{
    /// <summary>
    ///     The name of the provider.
    /// </summary>
    public string ProviderName { get; } = providerName;
}