using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.Messages;
using Flowly.MessageInfrastructure.RecurringJobs;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs.Registration;

public static class RecurringJobHandlerRegistrationExtensions
{
    public static IFlowlyBuilder AddRecurringJob<TRecurringJob>(
        this IFlowlyBuilder flowlyBuilder) where TRecurringJob : class, IRecurringJobHandler
    {
        var resolvedOptions = RecurringJobHandlerOptionsResolver.Resolve<TRecurringJob>();

        flowlyBuilder.AddQueueRegistration(JobQueuesNames.RecurringJobs, requiresSession: true);
        
        flowlyBuilder.Services.AddSingleton(new RecurringJobHandlerBackgroundService<TRecurringJob>.RecurringJobSettings(
            resolvedOptions.JobDescription, 
            typeof(TRecurringJob).Name, 
            resolvedOptions.CronExpression));

        flowlyBuilder.Services.AddHostedService<RecurringJobHandlerBackgroundService<TRecurringJob>>();

        flowlyBuilder.Services.AddScoped<TRecurringJob>();

        flowlyBuilder.AddMessageSubmitter<CreateRecurringJobState>();

        return flowlyBuilder;
    }
}