using System.Diagnostics;
using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

[DebuggerDisplay("{TopicName}/{SubscriptionName}")]
internal class EventDescription(string topicOrExchangeName, string? subscriptionName, TimeSpan? defaultMessageTimeToLive, bool? deadLetterOnMessageExpiration) : IEventDescription
{
    public string TopicName { get; } = topicOrExchangeName;

    public string? SubscriptionName { get; } = subscriptionName;

    public TimeSpan? DefaultMessageTimeToLive { get; } = defaultMessageTimeToLive;

    public bool? DeadLetterOnMessageExpiration { get; } = deadLetterOnMessageExpiration;
}