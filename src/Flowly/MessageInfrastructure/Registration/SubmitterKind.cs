namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Identifies the kind of submitter registered in the Flowly framework, used by the dashboard
///     to route submit requests to the correct sender abstraction.
/// </summary>
public enum SubmitterKind
{
    /// <summary>Fire-and-forget message sent via <see cref="IMessageSender" />.</summary>
    Message,

    /// <summary>Fan-out event published via <see cref="IEventSender" />.</summary>
    Event,

    /// <summary>RPC-style blocking call dispatched via <see cref="IMessageCaller" />.</summary>
    Call,

    /// <summary>Job enqueued via <c>IJobMessageSender</c>, returning a <c>JobId</c> that can be used to track execution progress.</summary>
    Job,

    /// <summary>Stream message recorded via <see cref="IMessageRecorder" /> onto an append-only, replayable stream queue.</summary>
    Stream
}
