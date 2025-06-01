namespace SimpleTransit.MessageInfrastructure.Model;

internal record 
    HandlerSettings<TMessage>(string QueueName, string HandlerName, bool ReadAndDelete, int MaxConcurrentCalls = 1);