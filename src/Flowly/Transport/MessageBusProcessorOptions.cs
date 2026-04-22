namespace Flowly.Transport;

public record MessageBusProcessorOptions(int MaxConcurrentCalls, MessageBusReceiveMode ReceiveMode);