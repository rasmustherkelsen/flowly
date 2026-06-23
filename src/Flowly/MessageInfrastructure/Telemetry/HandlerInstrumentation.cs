using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Flowly.MessageInfrastructure.Telemetry;

/// <summary>
///     Active implementation of <see cref="IHandlerInstrumentation" /> that emits OTel metrics via
///     <see cref="FlowlyInstrumentationConstants.MeterName" /> and traces via
///     <see cref="FlowlyInstrumentationConstants.ActivitySourceName" />. Registered when
///     <see cref="FlowlyOptions.EnableTelemetry" /> is <see langword="true" />.
/// </summary>
internal sealed class HandlerInstrumentation : IHandlerInstrumentation, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _received;
    private readonly Counter<long> _succeeded;
    private readonly Counter<long> _failed;
    private readonly Counter<long> _retried;
    private readonly Histogram<double> _duration;
    private readonly Counter<long> _replied;
    private readonly Counter<long> _replyFailed;
    private readonly Histogram<double> _replyDuration;

    /// <summary>
    ///     Initialises the OTel meter and creates all metric instruments.
    /// </summary>
    public HandlerInstrumentation()
    {
        _meter = new Meter(FlowlyInstrumentationConstants.MeterName);
        _received = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.HandlerMessagesReceived);
        _succeeded = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.HandlerMessagesSucceeded);
        _failed = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.HandlerMessagesFailed);
        _retried = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.HandlerMessagesRetried);
        _duration = _meter.CreateHistogram<double>(FlowlyInstrumentationConstants.HandlerProcessingDuration, "ms");
        _replied = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.CallHandlerReplied);
        _replyFailed = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.CallHandlerReplyFailed);
        _replyDuration = _meter.CreateHistogram<double>(FlowlyInstrumentationConstants.CallHandlerReplyDuration, "ms");
    }

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, ActivityContext parentContext = default)
        => FlowlyInstrumentationConstants.ActivitySource.StartActivity(
            $"flowly.handle {queueName}",
            ActivityKind.Consumer,
            parentContext,
            [
                new KeyValuePair<string, object?>("handler", handlerName),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingSystem, messagingSystem),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingDestinationName, queueName),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingOperationType, "process"),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingMessageId, messageProperties.MessageId),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingMessageConversationId, messageProperties.CorrelationId),
            ]);

    /// <inheritdoc />
    public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, IEnumerable<ActivityLink> links)
        => FlowlyInstrumentationConstants.ActivitySource.StartActivity(
            $"flowly.handle {queueName}",
            ActivityKind.Consumer,
            parentContext: default,
            tags:
            [
                new KeyValuePair<string, object?>("handler", handlerName),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingSystem, messagingSystem),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingDestinationName, queueName),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingOperationType, "process"),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingMessageId, messageProperties.MessageId),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingMessageConversationId, messageProperties.CorrelationId),
            ],
            links: links);

    /// <inheritdoc />
    public void RecordReceived(string handlerName, string queueName, long count = 1)
        => _received.Add(count, new TagList { { "handler", handlerName }, { FlowlyInstrumentationConstants.MessagingDestinationName, queueName } });

    /// <inheritdoc />
    public void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1)
    {
        Activity.Current?.SetTag("outcome", "success");

        var tags = new TagList { { "handler", handlerName }, { FlowlyInstrumentationConstants.MessagingDestinationName, queueName } };

        _succeeded.Add(count, tags);
        _duration.Record(durationMs, tags);
    }

    /// <inheritdoc />
    public void RecordFailed(string handlerName, string queueName, long count = 1)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error);
        Activity.Current?.SetTag("outcome", "failed");

        _failed.Add(count, new TagList { { "handler", handlerName }, { FlowlyInstrumentationConstants.MessagingDestinationName, queueName } });
    }

    /// <inheritdoc />
    public void RecordRetried(string handlerName, string queueName, long count = 1)
    {
        Activity.Current?.SetTag("outcome", "retry");

        _retried.Add(count, new TagList { { "handler", handlerName }, { FlowlyInstrumentationConstants.MessagingDestinationName, queueName } });
    }

    /// <inheritdoc />
    public Activity? StartSendingResponse(string callQueueName, string replyQueueName, string messagingSystem, string messageId, string correlationId)
        => FlowlyInstrumentationConstants.ActivitySource.StartActivity(
            $"flowly.call.reply {callQueueName}",
            ActivityKind.Producer,
            FlowlyInstrumentationConstants.ResolveProducerParentContext(),
            [
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingSystem, messagingSystem),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingDestinationName, replyQueueName),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingOperationType, "publish"),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingMessageId, messageId),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingMessageConversationId, correlationId),
            ]);

    /// <inheritdoc />
    public void RecordResponseSent(string callQueueName, double durationMs)
    {
        Activity.Current?.SetTag("outcome", "success");

        var tags = new TagList { { FlowlyInstrumentationConstants.MessagingDestinationName, callQueueName } };

        _replied.Add(1, tags);
        _replyDuration.Record(durationMs, tags);
    }

    /// <inheritdoc />
    public void RecordResponseFailed(string callQueueName)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error);
        Activity.Current?.SetTag("outcome", "failed");

        _replyFailed.Add(1, new TagList { { FlowlyInstrumentationConstants.MessagingDestinationName, callQueueName } });
    }

    /// <summary>Disposes the underlying OTel <see cref="Meter" />.</summary>
    public void Dispose() => _meter.Dispose();
}
