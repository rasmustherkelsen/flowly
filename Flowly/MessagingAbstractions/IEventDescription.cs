namespace Flowly.MessagingAbstractions;

public interface IEventDescription
{
    string TopicOrExchangeName { get; }

    string SubscriptionName { get; }

    TimeSpan? DefaultMessageTimeToLive { get; }

    bool? DeadLetterOnMessageExpiration { get; }
}
