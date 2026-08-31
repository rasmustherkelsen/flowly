namespace Flowly.DeadLetters.Telemetry;

/// <summary>
///     Instrumentation for the periodic dead letter retention cleanup sweep. Two implementations are registered
///     depending on <see cref="Flowly.FlowlyOptions.EnableTelemetry"/>: <c>DeadLetterCleanupInstrumentation</c>
///     (active) and <c>NullDeadLetterCleanupInstrumentation</c> (no-op).
/// </summary>
internal interface IDeadLetterCleanupInstrumentation
{
    /// <summary>Records that <paramref name="count"/> Pending dead letters were purged for exceeding retention.</summary>
    void RecordPendingPurged(int count);

    /// <summary>Records that <paramref name="count"/> stale Requeued audit-trail rows were purged for exceeding retention.</summary>
    void RecordRequeuedPurged(int count);
}
