using System.Diagnostics;
using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

[DebuggerDisplay("{Name} (reply)")]
internal class ReplyQueueDescription(string name) : IReplyQueueDescription
{
    public string Name { get; } = name;

    public TimeSpan DefaultMessageTimeToLive { get; } = TimeSpan.FromDays(1);

    public bool DeadLetterOnMessageExpiration { get; } = false;

    public TimeSpan LockDuration { get; } = TimeSpan.FromMinutes(5);

    public bool RequiresSession { get; } = false;
}
