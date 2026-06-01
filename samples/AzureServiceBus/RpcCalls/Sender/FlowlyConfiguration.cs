using Flowly;
using Flowly.AzureServiceBus;
using Messages;

namespace Sender;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseAzureServiceBus(connection: "AzureServiceBus");

        builder.AddCallSubmitter<MyMessage>();
    }
}
