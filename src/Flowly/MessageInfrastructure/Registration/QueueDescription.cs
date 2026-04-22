using System.Diagnostics;
using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

[DebuggerDisplay("{Name}")]
internal class QueueDescription(string name, TimeSpan defaultMessageTimeToLive, bool deadLetterOnMessageExpiration, TimeSpan lockDuration, bool requiresSession) : IQueueDescription
{
    public string Name { get; } = name;

    public TimeSpan DefaultMessageTimeToLive { get; } = defaultMessageTimeToLive;

    public bool DeadLetterOnMessageExpiration { get; } = deadLetterOnMessageExpiration;

    public TimeSpan LockDuration { get; } = lockDuration;

    public bool RequiresSession { get; } = requiresSession;
}