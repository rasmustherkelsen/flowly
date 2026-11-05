namespace Flowly;

/// <summary>
///     Describes the send a <see cref="SendStep{TMessageFilter}" /> is currently wrapping. The constructor is public so consumers
///     can unit test their steps without a running host.
/// </summary>
/// <typeparam name="TMessageFilter">
///     The message filter type of the step: the type, interface or base class the runtime type of the message is assignable to.
/// </typeparam>
public class SendStepContext<TMessageFilter>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SendStepContext{TMessageFilter}" /> class.
    /// </summary>
    /// <param name="message">The message being sent.</param>
    /// <param name="cancellationToken">The cancellation token of the send.</param>
    public SendStepContext(TMessageFilter message, CancellationToken cancellationToken)
    {
        Message = message;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    ///     Gets the message being sent, typed as the step's <typeparamref name="TMessageFilter" />. The runtime type of the message
    ///     is assignable to <typeparamref name="TMessageFilter" />.
    /// </summary>
    public TMessageFilter Message { get; }

    /// <summary>
    ///     Gets the cancellation token of the send. A step cannot replace it for the inner steps and the send.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}
