namespace Flowly;

/// <summary>
///     Represents the rest of the handler step chain: the next inner <see cref="HandlerStep{TMessageFilter}" /> that matches the
///     message or, for the innermost step, the handler's own <c>Handle</c> call. A step invokes it from
///     <see cref="HandlerStep{TMessageFilter}.Handle" /> to continue processing.
/// </summary>
/// <remarks>
///     The delegate is parameterless on purpose: a step cannot replace the message or the cancellation token passed to the inner
///     steps and the handler. A step that never invokes the delegate skips the handler and every inner step.
/// </remarks>
/// <returns>A task that completes when the inner steps and the handler have completed.</returns>
public delegate Task HandlerStepDelegate();
