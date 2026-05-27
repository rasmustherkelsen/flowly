using Flowly;
#if (UseRabbitMQ)
using Flowly.RabbitMQ;
#endif
#if (UseAzureServiceBus)
using Flowly.AzureServiceBus;
#endif
#if (UseInMemory)
using Flowly.InMemory;
#endif

namespace FlowlyApp;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
#if (UseRabbitMQ)
        builder.UseRabbitMq(connection: "RabbitMQ");
#endif
#if (UseAzureServiceBus)
        builder.UseAzureServiceBus(connection: "AzureServiceBus");
#endif
#if (UseInMemory)
        builder.UseInMemory();
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
        builder.AddSqlServerDeadLetterTracking(
            builder.Configuration.GetConnectionString("FlowlyDeadLetters")!,
            enableMigrations: true);
#endif
#if (UseDeadLetterTracking && UsePostgres)
        builder.AddPostgresDeadLetterTracking(
            builder.Configuration.GetConnectionString("FlowlyDeadLetters")!,
            enableMigrations: true);
#endif
#if (UseDeadLetterTracking && UseSQLite)
        builder.AddSQLiteDeadLetterTracking(
            builder.Configuration.GetConnectionString("FlowlyDeadLetters")!,
            enableMigrations: true);
#endif
    }
}
