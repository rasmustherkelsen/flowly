using System.Diagnostics;
using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

[DebuggerDisplay("{TopicName}/{SubscriptionName}")]
internal class EventDescription(string topicName, string subscriptionName, TimeSpan? defaultMessageTimeToLive, bool? deadLetterOnMessageExpiration) : IEventSubscriptionDescription
{
    public string TopicName { get; } = topicName;

    public string SubscriptionName { get; } = subscriptionName;

    public TimeSpan? DefaultMessageTimeToLive { get; } = defaultMessageTimeToLive;

    public bool? DeadLetterOnMessageExpiration { get; } = deadLetterOnMessageExpiration;
}