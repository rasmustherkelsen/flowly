using Flowly;
using Flowly.AzureServiceBus;
using Flowly.Jobs;
using MessageContracts;

namespace Dashboard;

internal class DashboardFlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("EmulatorNamespace")
            .AddSqlServerDeadLetterTracking("FlowlyDeadLetters", false)
            .AddJobStateTrackingClient("FlowlyJobs")
            .AddJobSubmitter<ProcessOrder>()
            .AddMessageSubmitter<RebuildIndexMessage>()
            .AddMessageSubmitter<SomeQueryMessage>();
    }
}