using Flowly.MessageInfrastructure.Senders;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

public static class SubmitterRegistrationExtensions
{
    public static IServiceCollection AddMessageSubmitter<TMessage>(this IServiceCollection services, string queueName)
    {
        if (services.Any(s => s.ImplementationType == typeof(MessageSubmitter<TMessage>)))
            return services;

        services
            .AddSingleton(new MessageSubmitter<TMessage>.QueueSettings(queueName))
            .AddSingleton<IMessageSubmitter<TMessage>, MessageSubmitter<TMessage>>();

        if (services.Any(s => s.ImplementationType == typeof(MessageSender)))
            return services;

        services.AddSingleton<IMessageSender, MessageSender>();

        return services;
    }
}