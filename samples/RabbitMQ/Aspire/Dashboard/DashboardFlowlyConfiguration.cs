using Flowly;
using Flowly.Jobs;
using Flowly.RabbitMQ;
using MessageContracts;

namespace Dashboard;

internal class DashboardFlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseRabbitMq(connection: "RabbitMQ")
            .AddPostgresDeadLetterTracking(builder.Configuration.GetConnectionString("FlowlyDeadLetters")!, false)
            .AddJobStateTrackingClient(builder.Configuration.GetConnectionString("FlowlyJobs")!)
            .AddJobSubmitter<ProcessOrder>()
            .AddMessageSubmitter<RebuildIndexMessage>()
            .AddMessageSubmitter<SomeQueryMessage>();
    }
}