using System.Diagnostics;
using Flowly.MessageInfrastructure.Events.Telemetry;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.SendSteps;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Events;

internal class EventSubmitter<TEvent>(
    IMessageBusClientRegistry clientRegistry,
    string topicName,
    string providerName,
    IEventPublisherInstrumentation publisherInstrumentation,
    ISendStepRunner sendStepRunner) : IEventSubmitter<TEvent>
{
    public Task Raise(TEvent @event, CancellationToken cancellationToken = default)
        => sendStepRunner.Run(@event!, cancellationToken, () => RaiseCore(@event, cancellationToken));

    private async Task RaiseCore(TEvent @event, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var client = clientRegistry.GetTransportClient(providerName);
        var eventCapableClient = client.Require<IEventCapableMessageBusClient>(providerName);

        var messageId = Guid.NewGuid().ToString();
        using var activity = publisherInstrumentation.StartRaising(topicName, client.MessagingSystem, messageId);
        activity.ApplyTagsFrom(@event);

        try
        {
            var publisher = await eventCapableClient.CreateEventPublisher(topicName);
            await publisher.SendMessage(@event, new MessageProperties(messageId, string.Empty), cancellationToken);
            publisherInstrumentation.RecordRaised(topicName, sw.Elapsed.TotalMilliseconds);
        }
        catch
        {
            publisherInstrumentation.RecordFailed(topicName);
            throw;
        }
    }
}