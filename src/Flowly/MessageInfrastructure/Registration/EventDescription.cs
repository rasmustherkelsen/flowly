using System.Diagnostics;
using Flowly.MessagingAbstractions;

namespace Flowly.MessageInfrastructure.Registration;

[DebuggerDisplay("{TopicOrExchangeName}/{SubscriptionName}")]
internal class EventDescription(string topicOrExchangeName, string subscriptionName, TimeSpan? defaultMessageTimeToLive, bool? deadLetterOnMessageExpiration) : IEventDescription
{
    public string TopicOrExchangeName { get; } = topicOrExchangeName;

    public string SubscriptionName { get; } = subscriptionName;

    public TimeSpan? DefaultMessageTimeToLive { get; } = defaultMessageTimeToLive;

    public bool? DeadLetterOnMessageExpiration { get; } = deadLetterOnMessageExpiration;
}
