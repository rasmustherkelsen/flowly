using Flowly.Jobs;
using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.Messages;
using Flowly.Jobs.RecurringJobs;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

/// <summary>
///     Provides extension methods for registering recurring job handlers in the Flowly framework.
/// </summary>
public static class RecurringJobHandlerRegistrationExtensions
{
    /// <summary>
    ///     Registers a recurring job handler of type <typeparamref name="TRecurringJob" />. The job description and CRON
    ///     expression are resolved from the <see cref="RecurringJobAttribute" /> applied to
    ///     <typeparamref name="TRecurringJob" />, or from an override in
    ///     <see cref="RecurringJobHandler.Configure(RecurringJobHandlerOptions)" />. The framework registers an
    ///     execution-lane queue for the recurring job and sets up the background service that triggers the handler
    ///     according to the CRON schedule. Requires <see cref="JobStateTrackerRegistrationExtension.AddJobStateTracking" />
    ///     to have been called so that the recurring job state is persisted.
    /// </summary>
    /// <param name="flowlyBuilder">The Flowly builder to register the recurring job with.</param>
    /// <typeparam name="TRecurringJob">The recurring job handler type. Must inherit <see cref="RecurringJobHandler" />.</typeparam>
    /// <returns>The same <see cref="IFlowlyBuilder" /> instance for further configuration.</returns>
    public static IFlowlyBuilder AddRecurringJob<TRecurringJob>(this IFlowlyBuilder flowlyBuilder) where TRecurringJob : RecurringJobHandler
    {
        var resolvedOptions = RecurringJobHandlerOptionsResolver.Resolve<TRecurringJob>();
        
        using var serviceProvider = flowlyBuilder.Services.BuildServiceProvider();
        var queueRegistrar = serviceProvider.GetRequiredService<IQueueRegistrar>();
        
        queueRegistrar.Register(
            flowlyBuilder.Services, 
            flowlyBuilder.TopologyNameResolver.ResolveQueueName<FlowlysysRecurringJobsMessage>(), 
            true, 
            ProviderNameResolver.GetRegistry(flowlyBuilder.Services).PrimaryProviderName);

        flowlyBuilder.Services
            .AddSingleton(new RecurringJobHandlerBackgroundService<TRecurringJob>.RecurringJobSettings(
                resolvedOptions.JobDescription,
                typeof(TRecurringJob).Name,
                resolvedOptions.CronExpression))
            .AddHostedService<RecurringJobHandlerBackgroundService<TRecurringJob>>()
            .AddScoped<TRecurringJob>();

        flowlyBuilder.AddMessageSubmitter<FlowlysysCreateRecurringJobStateMessage>();
        flowlyBuilder.AddJobStateSubmitters();

        return flowlyBuilder;
    }
}