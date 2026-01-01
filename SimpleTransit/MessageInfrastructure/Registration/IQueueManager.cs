namespace SimpleTransit.MessageInfrastructure.Registration
{
    public interface IQueueManager
    {
        void RegisterQueue(string queueName);
        IReadOnlyList<string> GetRegisteredQueues();
    }
}