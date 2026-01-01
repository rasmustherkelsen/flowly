namespace SimpleTransit.MessagingAbstractions;

public interface IMessageBusClient
{
    IMessageBusReceiver CreateReceiver(string queueName);
    
    IMessageBusProcessor<TMessage> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options);

    IExecutionLaneProcessor CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options);

    IMessageBusSender CreateMessageBusSender(string queueName);
}