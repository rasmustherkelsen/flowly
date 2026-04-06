using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Senders;
using Flowly.MessageInfrastructure;
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
                .AddMessageSubmitter<CreateJobState>()
                .AddMessageSubmitter<UpdateJobState>()
                .AddMessageSubmitter<JobFailed>()
                .AddMessageSubmitter<UpdateCustomJobState>()
                .AddMessageSubmitter<StartRecurringJobMessage>()
                .AddMessageSubmitter<JobIsAlive>();

            return flowlyBuilder;
        }

        public IFlowlyBuilder AddJobSubmitter<TMessage>() where TMessage : class, IJobMessage
        {
            if (flowlyBuilder.Services.Any(s => s.ImplementationType == typeof(JobSubmitter<TMessage>)))
                return flowlyBuilder;

            var queueName = MessageQueueNameResolver.Resolve<TMessage>();

            flowlyBuilder.Services
                .AddSingleton(new JobSubmitter<TMessage>.QueueSettings(queueName))
                .AddSingleton<IJobSubmitter<TMessage>, JobSubmitter<TMessage>>()
                .AddSingleton<IJobMessageSender, JobMessageSender>();

            flowlyBuilder.AddJobStateSubmitters();

            return flowlyBuilder;
        }
    }
}