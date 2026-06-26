using Flowly;
#if (UseRabbitMQ)
using Flowly.RabbitMQ;
#else
using Flowly.AzureServiceBus;
#endif
using Flowly.OpenTelemetry;
using MyAspireApp.Messages;

namespace MyAspireApp.DeadLetterTracker;

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

        builder.AddSqlServerDeadLetterTracking("FlowlyDeadLetters", enableMigrations: true);
#endif
#if (UsePostgres)

        builder.AddPostgresDeadLetterTracking("FlowlyDeadLetters", enableMigrations: true);
#endif
#if (UseSQLite)

        builder.AddSQLiteDeadLetterTracking("FlowlyDeadLetters", enableMigrations: true);
#endif

        builder.AddDeadLetterSource<DeadLetterSampleMessage>();

        builder.Services.AddOpenTelemetry()
            .WithMetrics(m => m.AddFlowlyInstrumentation())
            .WithTracing(t => t.AddFlowlyInstrumentation());
    }
}
