using Flowly;
#if (UseRabbitMQ)
using Flowly.RabbitMQ;
#else
using Flowly.AzureServiceBus;
#endif
using Flowly.OpenTelemetry;
#if (UseJobTracking)
using Flowly.Jobs;
#endif
using MyAspireApp.Messages;
using MyAspireApp.Receiver.Handlers;

namespace MyAspireApp.Receiver;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
#if (UseRabbitMQ)
        builder.UseRabbitMq(connection: "RabbitMQ");
#else
        builder.UseAzureServiceBus(connection: "AzureServiceBus");
#endif
#if (UseJobTracking && UseSqlServer)

        builder.AddSqlServerJobStateTracking("FlowlyJobs", enableMigrations: true);
#endif
#if (UseJobTracking && UsePostgres)

        builder.AddPostgresJobStateTracking("FlowlyJobs", enableMigrations: true);
#endif
#if (UseJobTracking && UseSQLite)

        builder.AddSQLiteJobStateTracking("FlowlyJobs", enableMigrations: true);
#endif
#if (UseDeadLetterTracking && UseSqlServer)

        builder.AddSqlServerDeadLetterTracking(builder.Configuration.GetConnectionString("FlowlyDeadLetters")!, enableMigrations: true);
#endif
#if (UseDeadLetterTracking && UsePostgres)

        builder.AddPostgresDeadLetterTracking(builder.Configuration.GetConnectionString("FlowlyDeadLetters")!, enableMigrations: true);
#endif
#if (UseDeadLetterTracking && UseSQLite)

        builder.AddSQLiteDeadLetterTracking(builder.Configuration.GetConnectionString("FlowlyDeadLetters")!, enableMigrations: true);
#endif

#if (UseCallHandler)
        builder.AddCallHandler<MyMessage, MyMessageHandler>();
#else
        builder.AddMessageHandler<MyMessage, MyMessageHandler>();
#endif
#if (UseJobTracking)

        builder.AddJobHandler<ProcessJobMessage, ProcessJobHandler>();
#endif
#if (UseDeadLetterTracking)

        builder.AddMessageHandler<DeadLetterSampleMessage, DeadLetterSampleMessageHandler>()
               .WithDeadLetterTracking();
#endif

        builder.Services.AddOpenTelemetry()
            .WithMetrics(m => m.AddFlowlyInstrumentation())
            .WithTracing(t => t.AddFlowlyInstrumentation());
    }
}
