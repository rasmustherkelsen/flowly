using Flowly.Jobs.Messages;
using Flowly.Jobs.Senders;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs;

/// <summary>
///     Provides extension methods for registering job submitters in the Flowly framework.
/// </summary>
public static class SubmitterRegistrationExtensions
{
    extension(IFlowlyBuilder flowlyBuilder)
    {
        internal IFlowlyBuilder AddJobStateSubmitters()
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

        /// <summary>
        ///     Registers a job submitter for <typeparamref name="TMessage" /> so that jobs of that type can be enqueued
        ///     using <see cref="IJobMessageSender.QueueJob{TMessage}" />. The target queue name is resolved from the
        ///     message contract via <see cref="QueueNameAttribute" /> or the auto-generated kebab-case convention. Also
        ///     registers the internal job state submitters required for lifecycle tracking. Calling this method more than
        ///     once for the same type is safe — subsequent calls are ignored.
        /// </summary>
        /// <typeparam name="TMessage">
        ///     The job message type. Must implement <see cref="IJobMessage" /> and be a reference type.
        /// </typeparam>
        /// <returns>The same <see cref="IFlowlyBuilder" /> instance for further configuration.</returns>
        public IFlowlyBuilder AddJobSubmitter<TMessage>() where TMessage : class, IJobMessage
        {
            if (flowlyBuilder.Services.Any(s => s.ImplementationType == typeof(JobSubmitter<TMessage>)))
                return flowlyBuilder;

            var queueName = flowlyBuilder.TopologyNameResolver.ResolveQueueName<TMessage>();
            var providerName = ProviderNameResolver.Resolve(flowlyBuilder.Services, typeof(TMessage));

            flowlyBuilder.Services
                .AddSingleton(new JobSubmitter<TMessage>.QueueSettings(queueName, providerName))
                .AddSingleton<IJobSubmitter<TMessage>, JobSubmitter<TMessage>>()
                .AddSingleton<IJobMessageSender, JobMessageSender>();

            FlowlySubmitterManifest.GetOrCreate(flowlyBuilder.Services)
                .Add(new DeferredSubmitterRegistration(typeof(TMessage), queueName, SubmitterKind.Job));

            flowlyBuilder.AddJobStateSubmitters();

            return flowlyBuilder;
        }
    }
}