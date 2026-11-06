namespace Flowly;

/// <summary>
///     Describes the delivery a <see cref="HandlerStep{TMessageFilter}" /> is currently wrapping. The constructor is public so
///     consumers can unit test their steps without a running host.
/// </summary>
/// <typeparam name="TMessageFilter">
///     The message filter type of the step: the type, interface or base class the runtime type of the message is assignable to.
/// </typeparam>
public class HandlerStepContext<TMessageFilter>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="HandlerStepContext{TMessageFilter}" /> class.
    /// </summary>
    /// <param name="message">The message being handled.</param>
    /// <param name="properties">The transport-level metadata that accompanies the message.</param>
    /// <param name="handlerType">The concrete handler class that will process the message.</param>
    /// <param name="cancellationToken">The cancellation token of the delivery.</param>
    public HandlerStepContext(TMessageFilter message, MessageProperties properties, Type handlerType, CancellationToken cancellationToken)
    {
        Message = message;
        Properties = properties;
        HandlerType = handlerType;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    ///     Gets the message being handled, typed as the step's <typeparamref name="TMessageFilter" />. The runtime type of the
    ///     message is assignable to <typeparamref name="TMessageFilter" />.
    /// </summary>
    public TMessageFilter Message { get; }

    /// <summary>
    ///     Gets the transport-level metadata of the message, such as <see cref="MessageProperties.MessageId" />,
    ///     <see cref="MessageProperties.CorrelationId" />, <see cref="MessageProperties.RetryCount" /> and
    ///     <see cref="MessageProperties.DeliveryCount" />.
    /// </summary>
    public MessageProperties Properties { get; }

    /// <summary>
    ///     Gets the concrete handler class that processes the message. This is a <see cref="Type" />, not a kind enumeration, so a
    ///     step can inspect the handler's attributes or compare it to a specific handler.
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    ///     Gets the cancellation token of the delivery. A step cannot replace it for the inner steps and the handler.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}
