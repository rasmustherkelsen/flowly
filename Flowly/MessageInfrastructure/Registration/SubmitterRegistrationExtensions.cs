using Flowly.MessageInfrastructure.Messages;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Senders;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

public static class SubmitterRegistrationExtensions
{
    public static IServiceCollection AddJobStateSubmitters(this IServiceCollection services)
    {
        services
            .AddMessageSubmitter<CreateJobState>(QueuesNames.CreateJobState)
            .AddMessageSubmitter<UpdateJobState>(QueuesNames.UpdateJobState)
            .AddMessageSubmitter<JobFailed>(QueuesNames.JobFailed)
            .AddMessageSubmitter<UpdateCustomJobState>(QueuesNames.UpdateJobCustomState)
            .AddMessageSubmitter<StartRecurringJobMessage>(QueuesNames.StartRecurringJob);

        return services;
    }

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

    public static IServiceCollection AddJobSubmitter<TMessage>(this IServiceCollection services, string queueName) where TMessage : class, IJobMessage
    {
        if (services.Any(s => s.ImplementationType == typeof(JobSubmitter<TMessage>)))
            return services;

        services
            .AddSingleton(new JobSubmitter<TMessage>.QueueSettings(queueName))
            .AddSingleton<IJobSubmitter<TMessage>, JobSubmitter<TMessage>>()
            .AddJobStateSubmitters();

        return services;
    }
}