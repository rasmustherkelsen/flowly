namespace Flowly.MessagingAbstractions;

public interface IMessageBusClient
{
    Task<IMessageBusReceiver> CreateReceiver(string queueName);

    Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options);

    Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options);

    Task<IMessageBusSender> CreateMessageBusSender(string queueName);

    Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName);

    Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default);
}