namespace Flowly.DeadLetters.BackgroundServices;

internal record EventSubscriptionDeadLetterIngestionSettings(string TopicOrExchangeName, string SubscriptionName, string ProviderName)
{
    public string DisplayName => $"{TopicOrExchangeName}/{SubscriptionName}";
}
