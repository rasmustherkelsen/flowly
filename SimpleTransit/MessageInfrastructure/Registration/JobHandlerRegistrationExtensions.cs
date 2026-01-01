using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimpleTransit.DatabaseModel.JobStateDatabase;
using SimpleTransit.MessageInfrastructure.BackgroundServices;
using SimpleTransit.MessageInfrastructure.Maintenance;
using SimpleTransit.MessageInfrastructure.MessageHandler;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Receivers;
using SimpleTransit.MessageInfrastructure.Senders;
using SimpleTransit.Repositories;
using SimpleTransit.Services;

namespace SimpleTransit.MessageInfrastructure.Registration;

public static class JobHandlerRegistrationExtensions
{
    public static ISimpleTransitBuilder AddJobStateTracking(
        this ISimpleTransitBuilder simpleTransitBuilder,
        string jobStateDatabaseConnectionString,
        bool enableMigrations = true)
    {
        simpleTransitBuilder.AddRepositories(jobStateDatabaseConnectionString);

        if (enableMigrations)
        {
            simpleTransitBuilder.Services.AddJobHandlerStateDatabaseMigrations();
        }

        simpleTransitBuilder.Services.AddJobMaintenanceBackgroundJobs();
        simpleTransitBuilder.Services.RegisterJobStateQueueProcessor();

        return simpleTransitBuilder;
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

    private static ISimpleTransitBuilder AddRepositories(this ISimpleTransitBuilder simpleTransitBuilder, string connectionString)
    {
        simpleTransitBuilder.Services.AddDbContextFactory<JobStateDataContext>(options =>
        {
            options.UseSqlServer(
                connectionString,
                sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null));
        });

        simpleTransitBuilder.Services.AddScoped<IJobStateRepository, JobStateRepository>();
        simpleTransitBuilder.Services.AddScoped<IJobStateQueryRepository, JobStateQueryRepository>();

        return simpleTransitBuilder;
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
            .AddRecurringJob<RemoveOldJobsRecurringJob>("Remove Old Jobs", TimeSpan.FromHours(1))
            .AddRecurringJob<FailHungJobsRecurringJob>("Fail hung jobs", TimeSpan.FromMinutes(30));
    }
}