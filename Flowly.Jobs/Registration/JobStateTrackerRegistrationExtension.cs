using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.DatabaseModel;
using Flowly.Jobs.MessageHandlers;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;
using Flowly.Jobs.Senders;
using Flowly.Jobs.Services;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Flowly.Jobs.Registration;

public static class JobStateTrackerRegistrationExtension
{
    /// <summary>
    /// Add job state tracking to the application instance.
    /// Call this before registering a database provider (e.g., AddSqlServerJobStateTracking).
    /// </summary>
    public static IFlowlyBuilder AddJobStateTracking(this IFlowlyBuilder flowlyBuilder)
    {
        flowlyBuilder.Services.AddOptions<JobStateTrackingOptions>();

        flowlyBuilder
            .AddJobMaintenanceBackgroundJobs()
            .RegisterJobStateQueueProcessor();

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
                .AddMessageHandler<CreateRecurringJobState, CreateRecurringJobStateHandler>()
                .AddMessageHandler<JobIsAlive, JobIsAliveMessageHandler>();
        }

        return flowlyBuilder;
    }

    internal static IFlowlyBuilder AddRepositories(this IFlowlyBuilder flowlyBuilder, Action<DbContextOptionsBuilder> dbContextOptions)
    {
        flowlyBuilder.Services.AddDbContextFactory<JobStateDataContext>(dbContextOptions);
        flowlyBuilder.Services.AddScoped<IJobStateRepository, JobStateRepository>();
        flowlyBuilder.Services.AddScoped<IJobAliveStatusRepository, JobAliveStatusRepository>();
        flowlyBuilder.Services.AddScoped<ICustomJobStateRepository, CustomJobStateRepository>();
        flowlyBuilder.Services.AddScoped<IJobTrackingService, JobTrackingService>();

        return flowlyBuilder;
    }

    private static IFlowlyBuilder AddJobMaintenanceBackgroundJobs(this IFlowlyBuilder flowlyBuilder)
    {
        flowlyBuilder.Services.AddHostedService<JobMaintenanceBackgroundService>();
        return flowlyBuilder;
    }
}
