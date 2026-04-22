using System.Diagnostics;
using System.Diagnostics.Metrics;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.MessageInfrastructure.Events.Telemetry;

internal sealed class EventHandlerInstrumentation : IEventHandlerInstrumentation, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _received;
    private readonly Counter<long> _succeeded;
    private readonly Counter<long> _failed;
    private readonly Counter<long> _retried;
    private readonly Histogram<double> _duration;

    public EventHandlerInstrumentation()
    {
        _meter = new Meter(FlowlyInstrumentationConstants.MeterName);
        _received = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.EventHandlerMessagesReceived);
        _succeeded = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.EventHandlerMessagesSucceeded);
        _failed = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.EventHandlerMessagesFailed);
        _retried = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.EventHandlerMessagesRetried);
        _duration = _meter.CreateHistogram<double>(FlowlyInstrumentationConstants.EventHandlerProcessingDuration, "ms");
    }

    public bool IsEnabled => true;

    public Activity? StartHandling(string handlerName, string topicOrExchangeName, string messagingSystem, MessageProperties messageProperties, ActivityContext parentContext = default)
        => FlowlyInstrumentationConstants.ActivitySource.StartActivity(
            $"flowly.event.handle {topicOrExchangeName}",
            ActivityKind.Consumer,
            parentContext,
            [
                new KeyValuePair<string, object?>("handler", handlerName),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingSystem, messagingSystem),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingDestinationName, topicOrExchangeName),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingOperationType, "process"),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingMessageId, messageProperties.MessageId),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingMessageConversationId, messageProperties.CorrelationId),
            ]);

    public void RecordReceived(string handlerName, string topicOrExchangeName, long count = 1)
        => _received.Add(count, new TagList { { "handler", handlerName }, { FlowlyInstrumentationConstants.MessagingDestinationName, topicOrExchangeName } });

    public void RecordSucceeded(string handlerName, string topicOrExchangeName, double durationMs, long count = 1)
    {
        Activity.Current?.SetTag("outcome", "success");

        var tags = new TagList { { "handler", handlerName }, { FlowlyInstrumentationConstants.MessagingDestinationName, topicOrExchangeName } };

        _succeeded.Add(count, tags);
        _duration.Record(durationMs, tags);
    }

    public void RecordFailed(string handlerName, string topicOrExchangeName, long count = 1)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error);
        Activity.Current?.SetTag("outcome", "failed");

        _failed.Add(count, new TagList { { "handler", handlerName }, { FlowlyInstrumentationConstants.MessagingDestinationName, topicOrExchangeName } });
    }

    public void RecordRetried(string handlerName, string topicOrExchangeName)
    {
        Activity.Current?.SetTag("outcome", "retry");

        _retried.Add(1, new TagList { { "handler", handlerName }, { FlowlyInstrumentationConstants.MessagingDestinationName, topicOrExchangeName } });
    }

    public void Dispose() => _meter.Dispose();
}
