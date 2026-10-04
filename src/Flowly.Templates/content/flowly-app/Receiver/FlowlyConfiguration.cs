using Flowly;
#if (UseRabbitMQ)
using Flowly.RabbitMQ;
#else
using Flowly.AzureServiceBus;
#endif
#if (UseJobTracking)
using Flowly.Jobs;
#endif
#if (UseStream && UseStreamCheckpoint)
using Flowly.MessageStreams;
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

#if (UseCallHandler)
        builder.AddCallHandler<MyMessage, MyMessageHandler>();
#else
#if (UseStream)
#if (UseStreamCheckpointSqlServer)
        builder.AddSqlServerMessageStreamPersistence("MsgStreamDb");
#endif
#if (UseStreamCheckpointPostgres)
        builder.AddPostgresMessageStreamPersistence("MsgStreamDb");
#endif
#if (UseStreamCheckpointSQLite)
        builder.AddSQLiteMessageStreamPersistence("MsgStreamDb");
#endif
        builder.AddMessageStreamHandler<MyMessage, MyMessageHandler>();
#if (UseStreamCheckpoint)
        builder.UseMessageStreamCheckpoint<MyMessage>();
#endif
#else
        builder.AddMessageHandler<MyMessage, MyMessageHandler>();
#endif
#endif
#if (UseJobTracking)

        builder.AddJobHandler<ProcessJobMessage, ProcessJobHandler>();
#endif
#if (UseDeadLetterTracking)

        builder.AddMessageHandler<DeadLetterSampleMessage, DeadLetterSampleMessageHandler>();
#endif
    }
}
