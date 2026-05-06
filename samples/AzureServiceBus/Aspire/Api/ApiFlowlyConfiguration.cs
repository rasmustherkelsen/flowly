using Flowly;
using Flowly.AzureServiceBus;
using Flowly.Jobs;
using MessageContracts;

namespace Api;

internal class ApiFlowlyConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("EmulatorNamespace")
            .AddSqlServerDeadLetterTracking(builder.Configuration.GetConnectionString("FlowlyDeadLetters")!, false)
            .AddJobStateTrackingClient(builder.Configuration.GetConnectionString("FlowlyJobs")!)
            .AddJobSubmitter<ProcessOrder>()
            .AddMessageSubmitter<RebuildIndexMessage>()
            .AddMessageSubmitter<SomeQueryMessage>();
    }
}