using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Senders;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs.Registration;

public static class SubmitterRegistrationExtensions
{
    extension(IFlowlyBuilder flowlyBuilder)
    {
        public IFlowlyBuilder AddJobStateSubmitters()
        {
            flowlyBuilder
                .AddMessageSubmitter<CreateJobState>(JobQueuesNames.CreateJobState)
                .AddMessageSubmitter<UpdateJobState>(JobQueuesNames.UpdateJobState)
                .AddMessageSubmitter<JobFailed>(JobQueuesNames.JobFailed)
                .AddMessageSubmitter<UpdateCustomJobState>(JobQueuesNames.UpdateJobCustomState)
                .AddMessageSubmitter<StartRecurringJobMessage>(JobQueuesNames.StartRecurringJob)
                .AddMessageSubmitter<JobIsAlive>(JobQueuesNames.JobIsAlive);

            return flowlyBuilder;
        }

        public IFlowlyBuilder AddJobSubmitter<TMessage>(string queueName) where TMessage : class, IJobMessage
        {
            if (flowlyBuilder.Services.Any(s => s.ImplementationType == typeof(JobSubmitter<TMessage>)))
                return flowlyBuilder;

            flowlyBuilder.Services
                .AddSingleton(new JobSubmitter<TMessage>.QueueSettings(queueName))
                .AddSingleton<IJobSubmitter<TMessage>, JobSubmitter<TMessage>>()
                .AddSingleton<IJobMessageSender, JobMessageSender>();
            
            flowlyBuilder.AddJobStateSubmitters();

            return flowlyBuilder;
        }
    }
}