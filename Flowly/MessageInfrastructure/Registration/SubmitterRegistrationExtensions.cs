using Flowly.MessageInfrastructure.Senders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flowly.MessageInfrastructure.Registration;

public static class SubmitterRegistrationExtensions
{
    public static IFlowlyBuilder AddMessageSubmitter<TMessage>(this IFlowlyBuilder flowlyBuilder)
    {
        if (flowlyBuilder.Services.Any(s => s.ImplementationType == typeof(MessageSubmitter<TMessage>)))
            return flowlyBuilder;

        var queueName = MessageQueueNameResolver.Resolve<TMessage>();
        var providerName = ProviderNameResolver.Resolve(flowlyBuilder.Services, typeof(TMessage));

        flowlyBuilder.Services
            .AddSingleton(new MessageSubmitter<TMessage>.QueueSettings(queueName, providerName))
            .AddSingleton<IMessageSubmitter<TMessage>, MessageSubmitter<TMessage>>();

        flowlyBuilder.Services.TryAddSingleton<IMessageSender, MessageSender>();

        return flowlyBuilder;
    }
}
