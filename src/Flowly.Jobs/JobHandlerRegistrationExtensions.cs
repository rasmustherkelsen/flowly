using Flowly.Jobs.BackgroundServices;
using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs;

/// <summary>
///     Provides extension methods for registering job handlers in the Flowly framework. Job handlers process messages
///     that implement <see cref="IJobMessage" /> and are tracked in the job state database.
/// </summary>
public static class JobHandlerRegistrationExtensions
{
    /// <summary>
    ///     Registers a job handler for the specified job message type and sets up the job state tracking infrastructure.
    ///     The handler will be invoked for each message of type <typeparamref name="TMessage" /> received from the queue,
    ///     and the job lifecycle (<c>Created → Started → Completed / Failed</c>) will be persisted to the database.
    ///     Requires <see cref="JobStateTrackerRegistrationExtension.AddJobStateTracking" /> to have been called and a
    ///     database backend (e.g. <c>AddSqlServerJobStateTracking</c>) to have been configured.
    /// </summary>
    /// <param name="flowlyBuilder">The Flowly builder to register the job handler with.</param>
    /// <typeparam name="TMessage">The job message type. Must implement <see cref="IJobMessage" />.</typeparam>
    /// <typeparam name="THandler">The job handler type. Must inherit <see cref="JobHandler{TMessage}" />.</typeparam>
    /// <returns>The same <see cref="IFlowlyBuilder" /> instance for further configuration.</returns>
    public static IFlowlyBuilder AddJobHandler<TMessage, THandler>(this IFlowlyBuilder flowlyBuilder)
        where THandler : JobHandler<TMessage>
        where TMessage : class, IJobMessage
    {
        flowlyBuilder.Services.AddScoped<JobHandler<TMessage>, THandler>();
        flowlyBuilder.AddMessageProcessor<TMessage, THandler, MessageProcessingBackgroundService<TMessage>, JobMessageHandlingStrategy<TMessage>>();

        flowlyBuilder.AddJobStateSubmitters();

        return flowlyBuilder;
    }
}