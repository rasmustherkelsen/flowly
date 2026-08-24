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

namespace Sender;

internal class FlowlyConfiguration : Configuration
{
#if (UseCallHandler)
    public override string? InstanceName => "sender";

#endif
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
#if (UseStream)
        builder.AddMessageRecorder<MyMessage>();
#else
        builder.AddMessageSubmitter<MyMessage>();
#endif
#endif
#if (UseDeadLetterTracking)
        builder.AddMessageSubmitter<DeadLetterSampleMessage>();
#endif
#if (UseJobTracking)
        builder.AddJobSubmitter<ProcessJobMessage>();
#endif
    }
}
