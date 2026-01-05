using Flowly.DatabaseModel.JobStateDatabase;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Maintenance;
using Flowly.MessageInfrastructure.MessageHandler;
using Flowly.MessageInfrastructure.Messages;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Senders;
using Flowly.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Flowly.Services;

namespace Flowly.MessageInfrastructure.Registration;

public static class JobHandlerRegistrationExtensions
{
    public static IFlowlyBuilder AddJobStateTracking(
        this IFlowlyBuilder flowlyBuilder,
        string jobStateDatabaseConnectionString,
        bool enableMigrations = true)
    {
        flowlyBuilder.AddRepositories(jobStateDatabaseConnectionString);

        if (enableMigrations)
        {
            flowlyBuilder.Services.AddJobHandlerStateDatabaseMigrations();
        }

        flowlyBuilder.Services.AddJobMaintenanceBackgroundJobs();
        flowlyBuilder.Services.RegisterJobStateQueueProcessor();

        return flowlyBuilder;
    }


    private static IServiceCollection RegisterJobStateQueueProcessor(this IServiceCollection services)
    {
        if (services.All(x => x.ImplementationType != typeof(RecurringJobSchedulerBackgroundService)))
        {
            services
                .AddSingleton(new RecurringJobSchedulerBackgroundServiceOptions(TimeSpan.FromSeconds(5)))
                .AddHostedService<RecurringJobSchedulerBackgroundService>()
                .AddScoped<IRecurringJobInvoker, RecurringJobInvoker>()
                .AddMessageHandler<CreateJobState, CreateJobStateHandler>(QueuesNames.CreateJobState)
                .AddMessageHandler<UpdateJobState, UpdateJobStateHandler>(QueuesNames.UpdateJobState)
                .AddMessageHandler<UpdateCustomJobState, UpdateCustomJobStateHandler>(QueuesNames.UpdateJobCustomState)
                .AddMessageHandler<JobFailed, JobFailedHandler>(QueuesNames.JobFailed)
                .AddMessageHandler<StartRecurringJobMessage, StartRecurringJobMessageHandler>(QueuesNames.StartRecurringJob)
                .AddMessageHandler<CreateRecurringJobState, CreateRecurringJobStateHandler>(QueuesNames.CreateRecurringJobState, Environment.ProcessorCount);
        }

        return services;
    }

    private static IFlowlyBuilder AddRepositories(this IFlowlyBuilder flowlyBuilder, string connectionString)
    {
        flowlyBuilder.Services.AddDbContextFactory<JobStateDataContext>(options =>
        {
            options.UseSqlServer(
                connectionString,
                sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null));
        });

        flowlyBuilder.Services.AddScoped<IJobStateRepository, JobStateRepository>();
        flowlyBuilder.Services.AddScoped<IJobStateQueryRepository, JobStateQueryRepository>();

        return flowlyBuilder;
    }

    public static IServiceCollection AddJobHandler<TMessage, THandler>(this IServiceCollection services, string queueName)
        where THandler : class, IJobMessageHandler<TMessage>
        where TMessage : class, IJobMessage
    {
        services.AddSingleton(new DeferredQueueRegistration(queueName));

        services
            .AddScoped<IJobMessageHandler<TMessage>, THandler>()
            .AddSingleton(new HandlerSettings<TMessage>(queueName, typeof(THandler).Name, true))
            .AddHostedService<ServiceBusJobHandlerBackgroundService<TMessage>>()
            .AddJobStateSubmitters();

        return services;
    }

    public static IServiceCollection AddMessageHandler<TMessage, THandler>(this IServiceCollection services, string queueName, int maxConcurrentCalls = 1)
        where THandler : class, IMessageHandler<TMessage>
        where TMessage : class
    {
        services.AddSingleton(new DeferredQueueRegistration(queueName));

        services
            .AddScoped<IMessageHandler<TMessage>, THandler>()
            .AddSingleton(new HandlerSettings<TMessage>(queueName, typeof(THandler).Name, false, maxConcurrentCalls))
            .AddHostedService<ServiceBusMessageHandlerBackgroundService<TMessage>>();

        return services;
    }

    public static IServiceCollection AddBatchMessageHandler<TMessage, THandler>(this IServiceCollection services, string queueName, int maxMessagesBeforeProcessing, TimeSpan maxWaitTime)
        where THandler : class, IBatchMessageHandler<TMessage>
        where TMessage : class
    {
        services.AddSingleton(new DeferredQueueRegistration(queueName));

        services
            .AddScoped<IBatchMessageHandler<TMessage>, THandler>()
            .AddSingleton(new ServiceBusMessageBatchHandlerBackgroundService<TMessage>.BatchQueueSettings(queueName, maxMessagesBeforeProcessing, maxWaitTime))
            .AddHostedService<ServiceBusMessageBatchHandlerBackgroundService<TMessage>>();
        return services;
    }

    private static IServiceCollection AddJobMaintenanceBackgroundJobs(this IServiceCollection services)
    {
        return services
            .AddRecurringJob<RemoveOldJobsRecurringJob>("Remove Old Jobs", "0 */1 * * *") // every hour
            .AddRecurringJob<FailHungJobsRecurringJob>("Fail hung jobs", "*/30 * * * *"); // every 30 minutes
    }
}