using Flowly.MessageInfrastructure.SendSteps;

namespace Flowly;

/// <summary>
///     Base class for application code that runs before a message leaves the sender, in the caller's process, for cross-cutting
///     concerns such as authorizing the current user before a command is published. Register a step once per Flowly configuration
///     with <see cref="SendStepRegistrationExtensions.AddSendStep{TSendStep}" />.
/// </summary>
/// <typeparam name="TMessageFilter">
///     Selects the messages the step runs for: the step runs only when the runtime type of the message is assignable to this type,
///     which can be an interface or a base class (for example <c>SendStep&lt;IRequiresAuthorization&gt;</c>). Use the non-generic
///     <see cref="SendStep" /> to run for every message.
/// </typeparam>
/// <remarks>
///     <para>
///         Send steps are a separate type from <see cref="HandlerStep{TMessageFilter}" />: sender and receiver are separate
///         deployables with separate configurations, so a step that exists on the sending side does not drag its dependencies into
///         the receiving process. The first registered step is the outermost.
///     </para>
///     <para>
///         <see cref="Handle" /> runs in the caller's process, so an exception thrown by a step reaches the caller of the send
///         unchanged: nothing is published and there is no retry. A step that returns without calling <c>next</c> silently drops
///         the send. Allowed, but throwing is the normal way to reject a message.
///     </para>
///     <para>
///         <b>Calls.</b> For <see cref="IMessageCaller.Call{TMessage,TReturn}" /> the steps run before the pending call is
///         registered and before the call timeout starts, so a step that throws rejects the call to the caller immediately instead
///         of ending in a timeout. A call needs a reply to return, so a step that returns without calling <c>next</c> makes the
///         call throw an <see cref="InvalidOperationException" /> stating that a send step prevented the call from being sent;
///         it does not silently return. Steps run outside the call's span and metrics, so the caller's failed metric records only
///         failures after the steps, the same as for the other senders. A step that throws is not counted as a failed call.
///     </para>
///     <para>
///         The senders are singletons, so a step cannot use the caller's dependency injection scope. Flowly creates a fresh scope
///         for each send, only when at least one step matches the message, and resolves the steps from it. Consequences:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 A scoped service that the caller's own middleware populated is a different, unpopulated instance inside a step.
///                 Take identity from a source that is not bound to the scope, such as <c>IHttpContextAccessor</c>. A good pattern
///                 is an <c>ICurrentUser</c> that is a thin reader of <c>IHttpContextAccessor.HttpContext?.User</c> and is never
///                 populated per request.
///             </description>
///         </item>
///         <item>
///             <description>
///                 A step cannot join the caller's <c>DbContext</c> or transaction, so send steps cannot implement a transactional
///                 outbox.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Outside an HTTP request there is no principal, so a step must handle that case explicitly.
///             </description>
///         </item>
///     </list>
///     <para>
///         <see cref="System.Diagnostics.Activity.Current" /> is async-local and flows through the fresh scope unchanged, so trace
///         context propagates normally. Flowly's own internal messages, such as job state and scheduling messages, never pass
///         through send steps.
///     </para>
/// </remarks>
public abstract class SendStep<TMessageFilter> : ISendStep, ISendStepInvoker
{
    /// <summary>
    ///     Executes the step around the rest of the chain. The default implementation calls <paramref name="next" />. Override to
    ///     inspect or reject the message before it is sent, for example by throwing, or to drop the send by not calling
    ///     <paramref name="next" />.
    /// </summary>
    /// <param name="context">The send being wrapped.</param>
    /// <param name="next">
    ///     Continues with the next inner step or, for the innermost step, performs the actual send. Not calling it drops the send
    ///     and skips all inner steps.
    /// </param>
    /// <returns>A task that completes when the step has finished.</returns>
    public virtual Task Handle(SendStepContext<TMessageFilter> context, SendStepDelegate next) => next();

    Task ISendStepInvoker.Invoke(object message, CancellationToken cancellationToken, SendStepDelegate next)
        => Handle(new SendStepContext<TMessageFilter>((TMessageFilter)message, cancellationToken), next);
}

/// <summary>
///     Base class for a send step that runs for every message. Equivalent to <c>SendStep&lt;object&gt;</c>; see
///     <see cref="SendStep{TMessageFilter}" /> for the behavior shared by all send steps.
/// </summary>
public abstract class SendStep : SendStep<object>;
