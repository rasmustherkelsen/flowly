using System.Diagnostics;
using Flowly.MessagingAbstractions;

namespace Flowly.MessageInfrastructure.Registration;

[DebuggerDisplay("{Name}")]
internal class QueueDescription(string name, TimeSpan defaultMessageTimeToLive, bool deadLetterOnMessageExpiration, TimeSpan lockDuration) : IQueueDescription
{
    public string Name { get; } = name;

    public TimeSpan DefaultMessageTimeToLive { get; } = defaultMessageTimeToLive;

    public bool DeadLetterOnMessageExpiration { get; } = deadLetterOnMessageExpiration;

    public TimeSpan LockDuration { get; } = lockDuration;
}