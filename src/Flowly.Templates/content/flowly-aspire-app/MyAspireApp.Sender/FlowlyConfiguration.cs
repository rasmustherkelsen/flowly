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

namespace MyAspireApp.Sender;

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
        builder.AddMessageSubmitter<MyMessage>();
#endif
#if (UseDeadLetterTracking)
        builder.AddMessageSubmitter<DeadLetterSampleMessage>();
#endif
#if (UseJobTracking)
        builder.AddJobSubmitter<ProcessJobMessage>();
#endif

        builder.Services.AddOpenTelemetry()
            .WithMetrics(m => m.AddFlowlyInstrumentation())
            .WithTracing(t => t.AddFlowlyInstrumentation());
    }
}
