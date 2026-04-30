using System.Diagnostics;
using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

[DebuggerDisplay("{TopicName}")]
internal class EventTopicDescription(string topicName, TimeSpan? defaultMessageTimeToLive, bool? deadLetterOnMessageExpiration) : IEventDescription
{
    public string TopicName { get; } = topicName;

    public TimeSpan? DefaultMessageTimeToLive { get; } = defaultMessageTimeToLive;

    public bool? DeadLetterOnMessageExpiration { get; } = deadLetterOnMessageExpiration;
}
