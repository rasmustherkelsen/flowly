using Flowly;
using Flowly.Jobs;
using Flowly.RabbitMQ;
using MessageContracts;

namespace Api;

internal class ApiFlowlyConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
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