using Flowly;
#if (UseRabbitMQ)
using Flowly.RabbitMQ;
#else
using Flowly.AzureServiceBus;
#endif
#if (UseJobTracking)
using Flowly.Jobs;
#endif
using Messages;

namespace Dashboard;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
#if (UseRabbitMQ)
        builder.UseRabbitMq(connection: "RabbitMQ");
#else
        builder.UseAzureServiceBus(connection: "AzureServiceBus");
#endif

#if (UseCallHandler)
        builder.AddCallSubmitter<MyMessage>();
#else
        builder.AddMessageSubmitter<MyMessage>();
#endif
#if (UseDeadLetterTracking)
        builder.AddMessageSubmitter<DeadLetterSampleMessage>();
#endif
#if (UseJobTracking)
        builder.AddJobSubmitter<ProcessJobMessage>();
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
    }
}
