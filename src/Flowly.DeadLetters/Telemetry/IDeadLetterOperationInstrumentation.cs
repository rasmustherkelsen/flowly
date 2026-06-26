using System.Diagnostics;

namespace Flowly.DeadLetters.Telemetry;

/// <summary>
///     Instrumentation for dead letter management operations (requeue and discard).
///     Two implementations are registered depending on <see cref="Flowly.FlowlyOptions.EnableTelemetry"/>:
///     <c>DeadLetterOperationInstrumentation</c> (active) and <c>NullDeadLetterOperationInstrumentation</c> (no-op).
/// </summary>
internal interface IDeadLetterOperationInstrumentation
{
    /// <summary>Starts a span for a requeue operation. Returns <see langword="null"/> when telemetry is disabled.</summary>
    Activity? StartRequeue(string queueName, string messageId, ActivityContext originalContext);

    /// <summary>Starts a span for a discard operation. Returns <see langword="null"/> when telemetry is disabled.</summary>
    Activity? StartDiscard(string queueName, string messageId, ActivityContext originalContext);

    /// <summary>Records a successful requeue.</summary>
    void RecordRequeued(string queueName);

    /// <summary>Records a discard with the given reason.</summary>
    void RecordDiscarded(string queueName, DeadLetterDiscardReason reason);
}
