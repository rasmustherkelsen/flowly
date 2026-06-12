namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Captures the metadata of a single submitter registration — the message type, the resolved queue or topic
///     name, and the kind of submitter. Collected into <see cref="FlowlySubmitterManifest" /> during
///     <c>AddMessageSubmitter&lt;T&gt;</c>, <c>AddEventSubmitter&lt;T&gt;</c>, and
///     <c>AddCallSubmitter&lt;T&gt;</c> calls and consumed by <c>Flowly.Dashboard</c> to expose dynamic
///     submission endpoints.
/// </summary>
/// <param name="MessageType">The CLR type of the message or event to submit.</param>
/// <param name="QueueOrTopicName">The resolved queue name (message/call) or topic name (event).</param>
/// <param name="Kind">The kind of submitter, which determines the sender abstraction used at runtime.</param>
public record DeferredSubmitterRegistration(
    Type MessageType,
    string QueueOrTopicName,
    SubmitterKind Kind);
