namespace Flowly.MessageInfrastructure.Callers;

internal class CallReturnQueueSettings<TReturn>(string returnQueueName, string providerName)
    where TReturn : class
{
    public string ReturnQueueName { get; } = returnQueueName;
    public string ProviderName { get; } = providerName;
}
