namespace Flowly.DeadLetters.BackgroundServices;

internal record EventSubscriptionDeadLetterIngestionSettings(string TopicName, string SubscriptionName, string ProviderName)
{
    public string DisplayName => $"{TopicName}/{SubscriptionName}";
}
