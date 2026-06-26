using Flowly;
#if (UseRabbitMQ)
using Flowly.RabbitMQ;
#else
using Flowly.AzureServiceBus;
#endif
using Flowly.OpenTelemetry;

namespace MyAspireApp.JobTracker;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
#if (UseRabbitMQ)
        builder.UseRabbitMq(connection: "RabbitMQ");
#else
        builder.UseAzureServiceBus(connection: "AzureServiceBus");
#endif
#if (UseSqlServer)

        builder.AddSqlServerJobStateTracking("FlowlyJobs", enableMigrations: true);
#endif
#if (UsePostgres)

        builder.AddPostgresJobStateTracking("FlowlyJobs", enableMigrations: true);
#endif
#if (UseSQLite)

        builder.AddSQLiteJobStateTracking("FlowlyJobs", enableMigrations: true);
#endif

        builder.Services.AddOpenTelemetry()
            .WithMetrics(m => m.AddFlowlyInstrumentation())
            .WithTracing(t => t.AddFlowlyInstrumentation());
    }
}
