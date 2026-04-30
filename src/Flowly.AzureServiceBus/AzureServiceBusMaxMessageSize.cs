namespace Flowly.AzureServiceBus;

/// <summary>
/// Message size limits in bytes for Azure Service Bus tiers.
/// Pass one of these values to <c>maxMessageSizeBytes</c> when calling <c>UseAzureServiceBus()</c>.
/// </summary>
public static class AzureServiceBusMaxMessageSize
{
    /// <summary>256 KB — the limit for the Standard tier.</summary>
    public const long Standard = 256 * 1024;

    /// <summary>1 MB — the default limit for the Premium tier.</summary>
    public const long Premium = 1 * 1024 * 1024;

    /// <summary>100 MB — the limit for Premium tier namespaces with the large message support feature enabled.</summary>
    public const long PremiumLargeMessages = 100 * 1024 * 1024;
}
