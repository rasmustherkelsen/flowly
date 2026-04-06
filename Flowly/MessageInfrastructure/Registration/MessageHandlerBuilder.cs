using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

internal class MessageHandlerBuilder<TMessage>(IFlowlyBuilder inner, string queueName) : IMessageHandlerBuilder<TMessage>
{
    public IServiceCollection Services => inner.Services;
    
    public IConfiguration Configuration => inner.Configuration;
    
    public string QueueName { get; } = queueName;
}