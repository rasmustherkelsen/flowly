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
using Receiver.Handlers;

namespace Receiver;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
#if (UseRabbitMQ)
        builder.UseRabbitMq(connection: "RabbitMQ");
#else
        builder.UseAzureServiceBus(connection: "AzureServiceBus");
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
    }
}
