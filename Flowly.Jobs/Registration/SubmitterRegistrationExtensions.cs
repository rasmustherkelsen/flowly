using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Senders;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs.Registration;

public static class SubmitterRegistrationExtensions
{
    internal static IServiceCollection AddJobStateSubmitters(this IServiceCollection services)
    {
        services
            .AddMessageSubmitter<CreateJobState>(QueuesNames.CreateJobState)
            .AddMessageSubmitter<UpdateJobState>(QueuesNames.UpdateJobState)
            .AddMessageSubmitter<JobFailed>(QueuesNames.JobFailed)
            .AddMessageSubmitter<UpdateCustomJobState>(QueuesNames.UpdateJobCustomState)
            .AddMessageSubmitter<StartRecurringJobMessage>(QueuesNames.StartRecurringJob);

        return services;
    }

    public static IServiceCollection AddJobSubmitter<TMessage>(this IServiceCollection services, string queueName) where TMessage : class, IJobMessage
    {
        if (services.Any(s => s.ImplementationType == typeof(JobSubmitter<TMessage>)))
            return services;

        services
            .AddSingleton(new JobSubmitter<TMessage>.QueueSettings(queueName))
            .AddSingleton<IJobSubmitter<TMessage>, JobSubmitter<TMessage>>()
            .AddSingleton<IJobMessageSender, JobMessageSender>()
            .AddJobStateSubmitters();

        return services;
    }
}