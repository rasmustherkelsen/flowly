using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.Messages;
using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.RecurringJobs;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs.Registration;

public static class RecurringJobHandlerRegistrationExtensions
{
    public static IServiceCollection AddRecurringJob<TRecurringJob>(
        this IServiceCollection services, 
        string jobDescription, 
        string cronExpression) where TRecurringJob : class, IRecurringJobHandler
    { 
        var recurringQueueExists = services.Any(sd => sd.ImplementationInstance is DeferredQueueRegistration { QueueName: QueuesNames.RecurringJobs });

        if (!recurringQueueExists)
        {
            services.AddSingleton(new DeferredQueueRegistration(QueuesNames.RecurringJobs));
        }
        
        services.AddSingleton(new RecurringJobHandlerBackgroundService<TRecurringJob>.RecurringJobSettings(
            jobDescription, 
            typeof(TRecurringJob).Name, 
            cronExpression));

        services.AddHostedService<RecurringJobHandlerBackgroundService<TRecurringJob>>();

        services.AddScoped<TRecurringJob>();

        services.AddMessageSubmitter<CreateRecurringJobState>(QueuesNames.CreateRecurringJobState);

        return services;
    }
}