using Flowly.AzureServiceBus;
using Flowly.Jobs.Registration;
using Flowly.MessageInfrastructure.Registration;
using MessageContracts;

namespace Api;

public class ApiFlowlyConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("EmulatorNamespace")
            .AddJobSubmitter<ProcessOrder>(QueuesNames.ProcessOrder)
            .AddMessageSubmitter<RebuildIndexMessage>(QueuesNames.RebuildIndex)
            .AddMessageSubmitter<SomeQueryMessage>(QueuesNames.SomeQuery);
    }
}