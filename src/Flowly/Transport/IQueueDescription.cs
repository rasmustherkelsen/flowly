namespace Flowly.Transport;

public interface IQueueDescription
{
    string Name { get; }

    TimeSpan DefaultMessageTimeToLive { get; }

    bool DeadLetterOnMessageExpiration { get; }

    TimeSpan LockDuration { get; }

    bool RequiresSession { get; }
}