namespace Flowly.Transport;

/// <summary>
///     Represents a message that has been received from the transport. Provides access to the message body and properties,
///     as well as methods for completing or dead-lettering the message. This class is used by the transport layer to read
///     messages where the format of the message varies. This is e.g. important for the recurring jobs feature.
/// </summary>
public interface IReceivedMessage
{
    /// <summary>
    ///     The properties of the received message, such as headers, correlation IDs, and other metadata. This allows handlers
    ///     to access and process message properties.
    /// </summary>
    public MessageProperties Properties { get; }

    /// <summary>
    ///     Gets the body of the received message as an object of type TBody. This method allows handlers to access the actual
    ///     content of the message in a strongly-typed manner, enabling them to process the message according to its specific
    ///     type and structure. The type parameter TBody allows for flexibility in handling different message formats and
    ///     types, making it easier for handlers to work with the message data without needing to perform manual
    ///     deserialization or type casting.
    /// </summary>
    /// <typeparam name="TBody"></typeparam>
    /// <returns>The body of the received message as an object of type TBody.</returns>
    public TBody GetBody<TBody>();
}

/// <summary>
///     Represents a message that has been received from the transport, with a strongly-typed body of type TMessage.
/// </summary>
/// <typeparam name="TMessage">The type of the message body.</typeparam>
public interface IReceivedMessage<TMessage>
{
    /// <summary>
    ///     Gets the body of the received message as an object of type TMessage. This property allows handlers to access the
    ///     actual message content.
    /// </summary>
    TMessage Body { get; }

    /// <summary>
    ///     The properties of the received message, such as headers, correlation IDs, and other metadata. This allows handlers
    ///     to access and process message properties.
    /// </summary>
    MessageProperties Properties { get; }

    /// <summary>
    ///     Completes the processing of the message, indicating that it has been successfully handled. This method should be
    ///     called when the message has been successfully processed and is no longer needed.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    Task Complete(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Dead-letters the message, indicating that it could not be processed successfully. This method should be called when
    ///     the message cannot be processed due to an error or other issue, and it should be moved to a dead-letter queue for
    ///     further analysis or handling. The optional reason parameter can be used to provide additional context about why the
    ///     message was dead-lettered, which can be helpful for troubleshooting and monitoring purposes.
    /// </summary>
    /// <param name="reason">The reason for dead-lettering the message, if applicable.</param>
    /// <param name="cancellationToken">
    ///     A cancellation token that can be used to cancel the operation or signal abortion of the
    ///     current process.
    /// </param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default);
}