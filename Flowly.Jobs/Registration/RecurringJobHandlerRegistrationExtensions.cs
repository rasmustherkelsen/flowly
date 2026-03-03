using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.Messages;
using Flowly.MessageInfrastructure.RecurringJobs;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs.Registration;

public static class RecurringJobHandlerRegistrationExtensions
{
    public static IFlowlyBuilder AddRecurringJob<TRecurringJob>(
        this IFlowlyBuilder flowlyBuilder, 
        string jobDescription, 
        string cronExpression) where TRecurringJob : class, IRecurringJobHandler
    {
        flowlyBuilder.AddQueueRegistration(JobQueuesNames.RecurringJobs, requiresSession: true);
        
        flowlyBuilder.Services.AddSingleton(new RecurringJobHandlerBackgroundService<TRecurringJob>.RecurringJobSettings(
            jobDescription, 
            typeof(TRecurringJob).Name, 
            cronExpression));

        flowlyBuilder.Services.AddHostedService<RecurringJobHandlerBackgroundService<TRecurringJob>>();

        flowlyBuilder.Services.AddScoped<TRecurringJob>();

        flowlyBuilder.AddMessageSubmitter<CreateRecurringJobState>(JobQueuesNames.CreateRecurringJobState);

        return flowlyBuilder;
    }
}