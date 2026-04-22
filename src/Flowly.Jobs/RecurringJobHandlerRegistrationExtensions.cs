using Flowly.Jobs;
using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.Messages;
using Flowly.MessageInfrastructure.RecurringJobs;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

public static class RecurringJobHandlerRegistrationExtensions
{
    public static IFlowlyBuilder AddRecurringJob<TRecurringJob>(
        this IFlowlyBuilder flowlyBuilder) where TRecurringJob : class, IRecurringJobHandler
    {
        var resolvedOptions = RecurringJobHandlerOptionsResolver.Resolve<TRecurringJob>();

        var primaryProviderName = ProviderNameResolver.GetRegistry(flowlyBuilder.Services).PrimaryProviderName;

        using var serviceProvider = flowlyBuilder.Services.BuildServiceProvider();
        var queueRegistrar = serviceProvider.GetRequiredService<IQueueRegistrar>();
        queueRegistrar.Register(flowlyBuilder.Services, JobQueuesNames.RecurringJobs, requiresSession: true, primaryProviderName);

        flowlyBuilder.Services
            .AddSingleton(new RecurringJobHandlerBackgroundService<TRecurringJob>.RecurringJobSettings(
                resolvedOptions.JobDescription,
                typeof(TRecurringJob).Name,
                resolvedOptions.CronExpression))
            .AddHostedService<RecurringJobHandlerBackgroundService<TRecurringJob>>()
            .AddScoped<TRecurringJob>();

        flowlyBuilder.AddMessageSubmitter<CreateRecurringJobState>();

        return flowlyBuilder;
    }
}
