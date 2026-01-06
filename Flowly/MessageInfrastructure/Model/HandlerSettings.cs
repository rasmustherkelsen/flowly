namespace Flowly.MessageInfrastructure.Model;

public record HandlerSettings<TMessage>(string QueueName, string HandlerName, bool ReadAndDelete, int MaxConcurrentCalls = 1);