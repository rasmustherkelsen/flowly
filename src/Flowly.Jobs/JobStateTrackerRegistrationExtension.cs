using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.DatabaseModel;
using Flowly.Jobs.MessageHandlers;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Repositories;
using Flowly.Jobs.Senders;
using Flowly.Jobs.Services;
using Flowly.Jobs.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs;

/// <summary>
///     Provides extension methods for registering the job state tracking infrastructure in the Flowly framework.
/// </summary>
public static class JobStateTrackerRegistrationExtension
{
    /// <summary>
    ///     Registers the core job state tracking services, background services, and internal message handlers. Must be
    ///     called before registering a database-specific provider (e.g. <c>AddSqlServerJobStateTracking</c> or
    ///     <c>AddPostgresJobStateTracking</c>), which supplies the EF Core context and repository implementations
    ///     required by the tracking infrastructure. Calling this method also registers the recurring job scheduler
    ///     background service and the job maintenance service that cleans up completed and failed jobs according to
    ///     <see cref="JobStateTrackingOptions" />.
    /// </summary>
    /// <param name="flowlyBuilder">The Flowly builder to register job state tracking with.</param>
    /// <returns>The same <see cref="IFlowlyBuilder" /> instance for further configuration.</returns>
    internal static IFlowlyBuilder AddJobStateTracking(this IFlowlyBuilder flowlyBuilder)
    {
        flowlyBuilder.Services.AddOptions<JobStateTrackingOptions>();

        flowlyBuilder.Services.AddSingleton<JobStateGaugeMetrics>();
        flowlyBuilder.Services.AddSingleton<IJobStateMetrics>(sp => sp.GetRequiredService<JobStateGaugeMetrics>());
        flowlyBuilder.Services.AddSingleton(new JobStateMetricsBackgroundServiceOptions(TimeSpan.FromSeconds(60)));
        flowlyBuilder.Services.AddHostedService<JobStateMetricsBackgroundService>();

        flowlyBuilder
            .AddJobMaintenanceBackgroundJobs()
            .RegisterJobStateQueueProcessor();

        return flowlyBuilder;
    }

    /// <summary>
    ///     This is the main infrastructure for handling the jobs in Flowly.
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
                .AddMessageHandler<FlowlysysCreateJobStateMessage, CreateJobStateHandler>()
                .AddMessageHandler<FlowlysysUpdateJobStateMessage, UpdateJobStateHandler>()
                .AddMessageHandler<FlowlysysUpdateJobCustomStateMessage, UpdateCustomJobStateHandler>()
                .AddMessageHandler<FlowlysysJobFailedMessage, JobFailedHandler>()
                .AddMessageHandler<FlowlysysStartRecurringJobMessage, StartRecurringJobMessageHandler>()
                .AddMessageHandler<FlowlysysCreateRecurringJobStateMessage, CreateRecurringJobStateHandler>()
                .AddMessageHandler<FlowlysysJobIsAliveMessage, JobIsAliveMessageHandler>();
        }

        return flowlyBuilder;
    }

    internal static IFlowlyBuilder AddRepositories(this IFlowlyBuilder flowlyBuilder, Action<DbContextOptionsBuilder> dbContextOptions)
    {
        flowlyBuilder.Services.AddDbContextFactory<JobStateDataContext>(dbContextOptions);
        flowlyBuilder.Services.AddSingleton<IJobStateCountReader, JobStateCountReader>();
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