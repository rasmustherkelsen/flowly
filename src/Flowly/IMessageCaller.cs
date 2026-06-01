namespace Flowly;

/// <summary>
///     Service for making blocking RPC-style calls via the Flowly messaging infrastructure. Resolved from the DI
///     container on the sender side after registering a call submitter with
///     <c>builder.AddCallSubmitter&lt;TMessage&gt;()</c>.
/// </summary>
/// <remarks>
///     <b>Return message attributes are ignored on the reply path.</b> The response is delivered via an
///     infrastructure reply queue; any attributes on the return message type (<c>[QueueName]</c>,
///     <c>[RetryPolicy]</c>, <c>[ProviderAffinity]</c>, etc.) are silently ignored for the reply leg.
/// </remarks>
public interface IMessageCaller
{
    /// <summary>
    ///     Sends <paramref name="message" /> to the remote call handler and waits for the response. Blocks until
    ///     the response is received or the configured timeout elapses, whichever comes first. The timeout is
    ///     determined by the per-submitter value set in <c>AddCallSubmitter</c>, falling back to
    ///     <see cref="FlowlyOptions.MessageCallTimeout" />.
    /// </summary>
    /// <param name="message">The call message to send.</param>
    /// <param name="cancellationToken">Token that can cancel the wait before the timeout.</param>
    /// <typeparam name="TMessage">
    ///     The call message type. Both <typeparamref name="TMessage" /> and <typeparamref name="TReturn" /> are
    ///     typically inferred from the argument — no explicit type arguments needed.
    /// </typeparam>
    /// <typeparam name="TReturn">
    ///     The response type declared by <typeparamref name="TMessage" /> via
    ///     <see cref="IReturns{TReturn}" />.
    /// </typeparam>
    /// <returns>The response produced by the remote <see cref="CallHandler{TMessage,TReturn}" />.</returns>
    /// <exception cref="OperationCanceledException">
    ///     Thrown when <paramref name="cancellationToken" /> is cancelled or the call times out.
    /// </exception>
    Task<TReturn> Call<TMessage, TReturn>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class, IReturns<TReturn>
        where TReturn : class;
}
