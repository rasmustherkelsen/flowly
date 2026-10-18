using System.Diagnostics;
using Flowly.MessageInfrastructure.Events.Telemetry;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Events;

internal class EventSubmitter<TEvent>(
    IMessageBusClientRegistry clientRegistry,
    EventSubmitter<TEvent>.TopicSettings topicSettings,
    IEventPublisherInstrumentation publisherInstrumentation) : IEventSubmitter<TEvent>
{
    public async Task Raise(TEvent @event, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var client = clientRegistry.GetTransportClient(topicSettings.ProviderName);
        var eventCapableClient = client.Require<IEventCapableMessageBusClient>(topicSettings.ProviderName);

        var messageId = Guid.NewGuid().ToString();
        using var activity = publisherInstrumentation.StartRaising(topicSettings.TopicName, client.MessagingSystem, messageId);
        activity.ApplyTagsFrom(@event);

        try
        {
            var publisher = await eventCapableClient.CreateEventPublisher(topicSettings.TopicName);
            await publisher.SendMessage(@event, new MessageProperties(messageId, string.Empty), cancellationToken);
            publisherInstrumentation.RecordRaised(topicSettings.TopicName, sw.Elapsed.TotalMilliseconds);
        }
        catch
        {
            publisherInstrumentation.RecordFailed(topicSettings.TopicName);
            throw;
        }
    }

    internal class TopicSettings(string topicName, string providerName)
    {
        public string TopicName { get; } = topicName;
        public string ProviderName { get; } = providerName;
    }
}