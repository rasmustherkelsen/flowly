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
public record MessageBusProcessorOptions(int MaxConcurrentCalls, MessageBusReceiveMode ReceiveMode);