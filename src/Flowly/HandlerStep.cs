using Flowly.MessageInfrastructure.HandlerSteps;

namespace Flowly;

/// <summary>
///     Base class for application code that wraps the user's <c>Handle</c> call for every handler a message reaches, for
///     cross-cutting concerns such as a logging scope, auditing, validation, ambient context, a unit of work or idempotency.
///     Register a step once per Flowly configuration with <see cref="HandlerStepRegistrationExtensions.AddHandlerStep{THandlerStep}" />.
/// </summary>
/// <typeparam name="TMessageFilter">
///     Selects the messages the step runs for: the step runs only when the runtime type of the message is assignable to this type,
///     which can be an interface or a base class (for example <c>HandlerStep&lt;IEnableAuditLog&gt;</c>). Use the non-generic
///     <see cref="HandlerStep" /> to run for every message.
/// </typeparam>
/// <remarks>
///     <para>
///         A step is a scoped service resolved from the same per-message dependency injection scope as the handler, so a step and
///         the handler share scoped services such as one <c>DbContext</c>. The first registered step is the outermost.
///     </para>
///     <para>
///         Steps wrap only the user's <c>Handle</c> call. Dead lettering of a corrupt body, retry republishing, completion, dead
///         lettering, job state messages and call replies stay outside the steps. An exception thrown by a step or the handler takes
///         Flowly's existing retry and dead letter path unchanged.
///     </para>
///     <para>
///         <b>A step that returns without calling <c>next</c> skips the handler and every inner step, and the delivery then
///         completes as a success. A step that catches an exception and does not rethrow also makes the delivery succeed.</b> Both
///         are intentional capabilities, for example to implement idempotency or to suppress a known benign failure.
///     </para>
///     <para>
///         <see cref="Handle" /> is the primitive. <see cref="OnBeforeHandlerExecution" /> and <see cref="OnAfterHandlerExecution" />
///         are optional conveniences built on it. <see cref="OnAfterHandlerExecution" /> runs only when <c>next</c> completed
///         without an exception; a step that needs failure handling overrides <see cref="Handle" /> and uses
///         <c>try</c>/<c>catch</c>/<c>finally</c>.
///     </para>
/// </remarks>
public abstract class HandlerStep<TMessageFilter> : IHandlerStep, IHandlerStepInvoker
{
    /// <summary>
    ///     Executes the step around the rest of the chain. The default implementation awaits
    ///     <see cref="OnBeforeHandlerExecution" />, then <paramref name="next" />, then <see cref="OnAfterHandlerExecution" />.
    ///     Override to take full control, for example to wrap the call in a <c>try</c>/<c>catch</c>/<c>finally</c>, to measure it
    ///     or to skip the handler by not calling <paramref name="next" />.
    /// </summary>
    /// <param name="context">The delivery being wrapped.</param>
    /// <param name="next">
    ///     Continues with the next inner step or, for the innermost step, the handler. Not calling it skips the handler and all inner
    ///     steps and the delivery completes as a success.
    /// </param>
    /// <returns>A task that completes when the step has finished.</returns>
    public virtual async Task Handle(HandlerStepContext<TMessageFilter> context, HandlerStepDelegate next)
    {
        await OnBeforeHandlerExecution(context);
        await next();
        await OnAfterHandlerExecution(context);
    }

    /// <summary>
    ///     Called by the default <see cref="Handle" /> before the rest of the chain runs. The default implementation does nothing.
    /// </summary>
    /// <param name="context">The delivery being wrapped.</param>
    /// <returns>A task that completes when the hook has finished.</returns>
    protected virtual Task OnBeforeHandlerExecution(HandlerStepContext<TMessageFilter> context) => Task.CompletedTask;

    /// <summary>
    ///     Called by the default <see cref="Handle" /> after the rest of the chain completed without an exception. It does not run
    ///     when an inner step or the handler throws. The default implementation does nothing.
    /// </summary>
    /// <param name="context">The delivery being wrapped.</param>
    /// <returns>A task that completes when the hook has finished.</returns>
    protected virtual Task OnAfterHandlerExecution(HandlerStepContext<TMessageFilter> context) => Task.CompletedTask;

    Task IHandlerStepInvoker.Invoke(
        object message,
        MessageProperties properties,
        Type handlerType,
        CancellationToken cancellationToken,
        HandlerStepDelegate next)
        => Handle(new HandlerStepContext<TMessageFilter>((TMessageFilter)message, properties, handlerType, cancellationToken), next);
}

/// <summary>
///     Base class for a handler step that runs for every message. Equivalent to <c>HandlerStep&lt;object&gt;</c>; see
///     <see cref="HandlerStep{TMessageFilter}" /> for the behavior shared by all steps.
/// </summary>
public abstract class HandlerStep : HandlerStep<object>;
