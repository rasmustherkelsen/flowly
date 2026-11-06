namespace Flowly;

/// <summary>
///     Represents the rest of the send step chain: the next inner <see cref="SendStep{TMessageFilter}" /> that matches the message
///     or, for the innermost step, the actual send of the message. A step invokes it from
///     <see cref="SendStep{TMessageFilter}.Handle" /> to continue sending.
/// </summary>
/// <remarks>
///     The delegate is parameterless on purpose: a step cannot replace the message or the cancellation token. A step that never
///     invokes the delegate silently drops the send, and every inner step is skipped.
/// </remarks>
/// <returns>A task that completes when the inner steps and the send have completed.</returns>
public delegate Task SendStepDelegate();
