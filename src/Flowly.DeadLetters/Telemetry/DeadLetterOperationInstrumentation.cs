using System.Diagnostics;
using System.Diagnostics.Metrics;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.DeadLetters.Telemetry;

internal sealed class DeadLetterOperationInstrumentation : IDeadLetterOperationInstrumentation, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _requeued;
    private readonly Counter<long> _discarded;

    public DeadLetterOperationInstrumentation()
    {
        _meter = new Meter(FlowlyInstrumentationConstants.MeterName);
        _requeued = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.DeadLettersRequeued);
        _discarded = _meter.CreateCounter<long>(FlowlyInstrumentationConstants.DeadLettersDiscarded);
    }

    public Activity? StartRequeue(string queueName, string messageId, ActivityContext originalContext)
    {
        var links = originalContext != default ? new[] { new ActivityLink(originalContext) } : null;

        return FlowlyInstrumentationConstants.ActivitySource.StartActivity(
            $"flowly.deadletter.requeue {queueName}",
            ActivityKind.Internal,
            FlowlyInstrumentationConstants.ResolveProducerParentContext(),
            [
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingDestinationName, queueName),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingMessageId, messageId),
            ],
            links);
    }

    public Activity? StartDiscard(string queueName, string messageId, ActivityContext originalContext)
    {
        var links = originalContext != default ? new[] { new ActivityLink(originalContext) } : null;

        return FlowlyInstrumentationConstants.ActivitySource.StartActivity(
            $"flowly.deadletter.discard {queueName}",
            ActivityKind.Internal,
            FlowlyInstrumentationConstants.ResolveProducerParentContext(),
            [
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingDestinationName, queueName),
                new KeyValuePair<string, object?>(FlowlyInstrumentationConstants.MessagingMessageId, messageId),
            ],
            links);
    }

    public void RecordRequeued(string queueName)
    {
        Activity.Current?.SetTag("outcome", "success");
        _requeued.Add(1, new TagList { { FlowlyInstrumentationConstants.MessagingDestinationName, queueName } });
    }

    public void RecordDiscarded(string queueName, DeadLetterDiscardReason reason)
    {
        Activity.Current?.SetTag("outcome", "success");
        _discarded.Add(1, new TagList
        {
            { FlowlyInstrumentationConstants.MessagingDestinationName, queueName },
            { "reason", ReasonToString(reason) },
        });
    }

    public void Dispose() => _meter.Dispose();

    private static string ReasonToString(DeadLetterDiscardReason reason) => reason switch
    {
        DeadLetterDiscardReason.UserInitiated => "user_initiated",
        DeadLetterDiscardReason.Expired => "expired",
        _ => reason.ToString().ToLowerInvariant(),
    };
}
