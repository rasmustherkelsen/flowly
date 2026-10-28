using Flowly;
#if (UseRabbitMQ)
using Flowly.RabbitMQ;
#elif (UseAzureEventHubs)
using Flowly.AzureEventHubs;
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
#if (UseCallHandler)
    public override string? InstanceName => "dashboard";

#endif
    public override void Configure(IFlowlyBuilder builder)
    {
#if (UseRabbitMQ)
        builder.UseRabbitMq(connection: "RabbitMQ");
#elif (UseAzureEventHubs)
        builder.UseAzureEventHubs(connection: "EventHubs");
#else
        builder.UseAzureServiceBus(connection: "AzureServiceBus");
#endif

#if (UseCallHandler)
        builder.AddCallSubmitter<MyMessage>();
#elif (!UseAzureEventHubs)
        builder.AddMessageSubmitter<MyMessage>();
#endif
#if (UseDeadLetterTracking)
        builder.AddMessageSubmitter<DeadLetterSampleMessage>();
#endif
#if (UseJobTracking)
        builder.AddJobSubmitter<ProcessJobMessage>();
#endif
#if (UseJobTracking && UseSqlServer)

        builder.AddJobStateTrackingClient("FlowlyJobs");
#endif
#if (UseJobTracking && UsePostgres)

        builder.AddJobStateTrackingClient("FlowlyJobs");
#endif
#if (UseJobTracking && UseSQLite)

        builder.AddJobStateTrackingClient("FlowlyJobs");
#endif
#if (UseDeadLetterTracking && UseSqlServer)

        builder.AddDeadLetterTrackingClient("FlowlyDeadLetters");
#endif
#if (UseDeadLetterTracking && UsePostgres)

        builder.AddDeadLetterTrackingClient("FlowlyDeadLetters");
#endif
#if (UseDeadLetterTracking && UseSQLite)

        builder.AddDeadLetterTrackingClient("FlowlyDeadLetters");
#endif
    }
}
