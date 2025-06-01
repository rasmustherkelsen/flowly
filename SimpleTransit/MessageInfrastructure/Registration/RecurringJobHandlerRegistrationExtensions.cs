using Microsoft.Extensions.DependencyInjection;
using SimpleTransit.MessageInfrastructure.BackgroundServices;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.RecurringJobs;

namespace SimpleTransit.MessageInfrastructure.Registration;

public static class RecurringJobHandlerRegistrationExtensions
{
    public static IServiceCollection AddRecurringJob<TRecurringJob>(
        this IServiceCollection services, 
        string jobDescription, 
        TimeSpan interval) where TRecurringJob : class, IRecurringJobHandler
    {
        services.AddSingleton(new RecurringJobHandlerBackgroundService<TRecurringJob>.RecurringJobSettings(
            jobDescription, 
            typeof(TRecurringJob).Name, 
            interval));

        services.AddHostedService<RecurringJobHandlerBackgroundService<TRecurringJob>>();

        services.AddScoped<TRecurringJob>();

        services.AddMessageSubmitter<CreateRecurringJobState>(QueuesNames.CreateRecurringJobState);

        return services;
    }
}