namespace Flowly.MessageInfrastructure.Events.Registration;

public record DeferredEventRegistration(
    string TopicOrExchangeName,
    string? SubscriptionName,
    TimeSpan? DefaultMessageTimeToLive = null,
    bool? DeadLetterOnMessageExpiration = null);
