using System.Text.RegularExpressions;

namespace Flowly.AzureEventHubs;

internal static partial class EventHubsConsumerGroup
{
    private const string DefaultConsumerGroup = "$Default";

    public static string FromConsumerName(string eventHubName, string? consumerName)
    {
        if (string.IsNullOrEmpty(consumerName))
            throw new ArgumentException($"A consumer name is required to read event hub '{eventHubName}': Flowly uses it as the Azure Event Hubs consumer group.", nameof(consumerName));

        if (consumerName != DefaultConsumerGroup && !ConsumerGroupRegex().IsMatch(consumerName))
            throw new InvalidOperationException(
                $"'{consumerName}' is not a valid Azure Event Hubs consumer group name, but it is the ConsumerName of a stream handler reading event hub '{eventHubName}', " +
                "and Flowly uses the ConsumerName as the consumer group. " +
                "A consumer group name is 1 to 50 characters of letters, digits, '.', '-' or '_', starting and ending with a letter or digit, and the handler type name is the default ConsumerName. " +
                $"Set ConsumerName on the stream handler to the name of an existing consumer group (\"{DefaultConsumerGroup}\" selects the default consumer group).");

        return consumerName;
    }

    [GeneratedRegex("^[A-Za-z0-9]([A-Za-z0-9._-]{0,48}[A-Za-z0-9])?$")]
    private static partial Regex ConsumerGroupRegex();
}
