using Flowly.MessageInfrastructure.Senders;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

public static class SubmitterRegistrationExtensions
{
    public static IFlowlyBuilder AddMessageSubmitter<TMessage>(this IFlowlyBuilder flowlyBuilder)
    {
        if (flowlyBuilder.Services.Any(s => s.ImplementationType == typeof(MessageSubmitter<TMessage>)))
            return flowlyBuilder;

        var queueName = MessageQueueNameResolver.Resolve<TMessage>();

        flowlyBuilder.Services
            .AddSingleton(new MessageSubmitter<TMessage>.QueueSettings(queueName))
            .AddSingleton<IMessageSubmitter<TMessage>, MessageSubmitter<TMessage>>();

        if (flowlyBuilder.Services.Any(s => s.ImplementationType == typeof(MessageSender)))
            return flowlyBuilder;

        flowlyBuilder.Services.AddSingleton<IMessageSender, MessageSender>();

        return flowlyBuilder;
    }
}