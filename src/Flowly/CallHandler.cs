namespace Flowly;

/// <summary>
///     Base class for call handlers. Inherit from this class to implement a remote-procedure-call style handler that
///     receives a message of type <typeparamref name="TMessage" /> and returns a response of type
///     <typeparamref name="TReturn" />. The Flowly infrastructure routes the response back to the originating caller
///     via a per-instance reply queue, identified by <see cref="IMessageCaller" />.
/// </summary>
/// <remarks>
///     The handler is registered on the normal call message queue and supports the same queue-level configuration as
///     <see cref="MessageHandler{TMessage}" />: retry policy (via <c>[RetryPolicy]</c> or
///     <see cref="Configure" />), queue name overrides, concurrency settings, and provider affinity.
///     <para>
///         <b>Return message attributes are ignored on the reply path.</b> Because <typeparamref name="TReturn" /> is
///         delivered directly to an infrastructure reply queue, any attributes on the return message type
///         (<c>[QueueName]</c>, <c>[RetryPolicy]</c>, <c>[ProviderAffinity]</c>, etc.) have no effect when the
///         message is sent as a response. Those attributes only apply if the same type is independently registered as a
///         normal message or event handler elsewhere.
///     </para>
/// </remarks>
/// <typeparam name="TMessage">
///     The type of message this handler processes. Must implement <see cref="IReturns{TReturn}" /> to declare its
///     response type.
/// </typeparam>
/// <typeparam name="TReturn">
///     The type of the response produced by this handler. Sent back to the caller via the reply queue.
///     Attributes on this type are <b>not</b> honoured for the reply path — see remarks.
/// </typeparam>
public abstract class CallHandler<TMessage, TReturn>
    where TMessage : class, IReturns<TReturn>
    where TReturn : class
{
    /// <summary>
    ///     Override to configure queue-level options in code. Behaves identically to
    ///     <see cref="MessageHandler{TMessage}.Configure" />: invoked during handler discovery and takes precedence
    ///     over attribute-based configuration.
    /// </summary>
    /// <param name="options">Queue options for the call message queue.</param>
    public virtual void Configure(HandlerQueueOptions options)
    {
    }

    /// <summary>
    ///     Override to implement the call handling logic. Called when a message of type
    ///     <typeparamref name="TMessage" /> is received. The returned <typeparamref name="TReturn" /> value is
    ///     automatically routed back to the originating caller.
    /// </summary>
    /// <param name="messageContext">Envelope containing the call message and cancellation token.</param>
    /// <returns>The response to send back to the caller.</returns>
    protected abstract Task<TReturn> Handle(IMessageContext<TMessage> messageContext);

    internal Task<TReturn> InvokeHandle(IMessageContext<TMessage> messageContext) => Handle(messageContext);
}
