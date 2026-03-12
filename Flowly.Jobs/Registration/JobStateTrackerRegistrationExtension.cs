using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.DatabaseModel;
using Flowly.Jobs.Maintenance;
using Flowly.Jobs.MessageHandlers;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;
using Flowly.Jobs.Senders;
using Flowly.Jobs.Services;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs.Registration;

public static class JobStateTrackerRegistrationExtension
{
    /// <summary>
    /// Add job state tracking to the application instance.
    /// </summary>
    public static IFlowlyBuilder AddJobStateTracking(
        this IFlowlyBuilder flowlyBuilder,
        string jobStateDatabaseConnectionString,
        bool enableMigrations = true)
    {
        flowlyBuilder.AddRepositories(options =>
            {
                options.UseSqlServer(
                    jobStateDatabaseConnectionString,
                    sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null));
            })
            .AddJobMaintenanceBackgroundJobs()
            .RegisterJobStateQueueProcessor();

        if (enableMigrations)
        {
            flowlyBuilder.Services.AddJobHandlerStateDatabaseMigrations();
        }

        return flowlyBuilder;
    }

    /// <summary>
    /// This is the main infrastructure for handling the jobs in Flowly.
    /// </summary>
    private static IFlowlyBuilder RegisterJobStateQueueProcessor(this IFlowlyBuilder flowlyBuilder)
    {
        if (flowlyBuilder.Services.All(x => x.ImplementationType != typeof(RecurringJobSchedulerBackgroundService)))
        {
            flowlyBuilder.Services
                .AddSingleton(new RecurringJobSchedulerBackgroundServiceOptions(TimeSpan.FromSeconds(5)))
                .AddHostedService<RecurringJobSchedulerBackgroundService>()
                .AddScoped<IRecurringJobInvoker, RecurringJobInvoker>();

            flowlyBuilder
                .AddMessageHandler<CreateJobState, CreateJobStateHandler>()
                .AddMessageHandler<UpdateJobState, UpdateJobStateHandler>()
                .AddMessageHandler<UpdateCustomJobState, UpdateCustomJobStateHandler>()
                .AddMessageHandler<JobFailed, JobFailedHandler>()
                .AddMessageHandler<StartRecurringJobMessage, StartRecurringJobMessageHandler>()
                .AddMessageHandler<CreateRecurringJobState, CreateRecurringJobStateHandler>(Environment.ProcessorCount)
                .AddMessageHandler<JobIsAlive, JobIsAliveMessageHandler>();
        }

        return flowlyBuilder;
    }

    private static IFlowlyBuilder AddRepositories(this IFlowlyBuilder flowlyBuilder, Action<DbContextOptionsBuilder> dbContextOptions)
    {
        flowlyBuilder.Services.AddDbContextFactory<JobStateDataContext>(dbContextOptions);
        flowlyBuilder.Services.AddScoped<IJobStateRepository, JobStateRepository>();
        flowlyBuilder.Services.AddScoped<IJobAliveStatusRepository, JobAliveStatusRepository>();
        flowlyBuilder.Services.AddScoped<ICustomJobStateRepository, CustomJobStateRepository>();

        return flowlyBuilder;
    }

    private static IFlowlyBuilder AddJobMaintenanceBackgroundJobs(this IFlowlyBuilder flowlyBuilder)
    {
        return flowlyBuilder
            .AddRecurringJob<RemoveOldJobsRecurringJob>()
            .AddRecurringJob<FailHungJobsRecurringJob>();
    }
}