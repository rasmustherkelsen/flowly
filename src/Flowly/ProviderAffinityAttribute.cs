namespace Flowly;

/// <summary>
///     Pins a message or event contract to a specific registered provider when multiple providers are registered. The
///     provider is identified by the name used when the provider was registered (for example the <c>name</c> argument of
///     <c>UseRabbitMq</c>). Without this attribute a contract is routed per capability (queue message, event, stream) to the
///     first registered provider that supports that capability, so a stream contract reaches a stream-capable provider and
///     an ordinary message reaches a queue-capable provider without any attribute. Use the attribute to override that
///     choice, for example to send a message to the second of two queue-capable providers. Registration fails with a
///     descriptive error when the named provider is not registered or does not support the capability the contract is
///     registered for.
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