namespace Flowly.Transport;

/// <summary>
///     Options passed to <see cref="IMessageBusClient.CreateProcessor{TMessage}" /> and
///     <see cref="IMessageBusClient.CreateExecutionLaneProcessor" /> when the framework creates a message processor.
///     Transport implementations must honour these settings when configuring the underlying broker client.
/// </summary>
/// <param name="MaxConcurrentCalls">
///     The maximum number of messages to process concurrently. Maps to
///     <see cref="HandlerQueueOptions.MaxConcurrentCalls" /> resolved from the handler's configuration.
/// </param>
/// <param name="ReceiveMode">
///     Whether messages should be locked during processing (<see cref="MessageBusReceiveMode.PeekLock" />) or
///     removed immediately upon receipt (<see cref="MessageBusReceiveMode.ReceiveAndDelete" />).
/// </param>
public record MessageBusProcessorOptions(int MaxConcurrentCalls, MessageBusReceiveMode ReceiveMode)
{
    /// <summary>
    ///     The logical name of the reader the processor or consumer is created for. Set by the stream background services
    ///     from the resolved <see cref="MessageStreamHandlerOptions.ConsumerName" /> (the handler type name unless overridden), the
    ///     same value that keys a registered <see cref="MessageStreamCheckpoint{TMessage}" />, when they call
    ///     <see cref="IStreamCapableMessageBusClient.CreateStreamProcessor{TMessage}" /> or
    ///     <see cref="IPartitionedStreamCapableMessageBusClient.CreatePartitionedStreamConsumer{TMessage}" />. A transport may use it
    ///     to identify the reader on the broker, for example as a broker-side consumer group; transports with no such concept
    ///     ignore it. <see langword="null" /> for processors that are not stream readers.
    /// </summary>
    public string? ConsumerName { get; init; }
}