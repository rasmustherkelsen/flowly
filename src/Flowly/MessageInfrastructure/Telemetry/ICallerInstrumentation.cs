namespace Flowly.MessageInfrastructure.Telemetry;

/// <summary>
///     Provides OpenTelemetry metrics instrumentation for <see cref="IMessageCaller" /> call operations. Measures
///     the end-to-end round-trip duration from <c>Call()</c> invocation to response receipt.
/// </summary>
internal interface ICallerInstrumentation
{
    /// <summary>
    ///     Increments the <c>flowly.call.succeeded</c> counter and records the end-to-end round-trip duration.
    ///     Called when a response is received before the timeout.
    /// </summary>
    /// <param name="callQueueName">The call queue name used as a metric tag.</param>
    /// <param name="durationMs">Total elapsed time in milliseconds from call start to response.</param>
    void RecordSucceeded(string callQueueName, double durationMs);

    /// <summary>
    ///     Increments the <c>flowly.call.failed</c> counter. Called when the call times out or throws.
    /// </summary>
    /// <param name="callQueueName">The call queue name used as a metric tag.</param>
    void RecordFailed(string callQueueName);
}
