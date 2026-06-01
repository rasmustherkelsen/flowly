using Flowly;
using Flowly.AzureServiceBus;
using Messages;
using Receiver.Handlers;

namespace Receiver;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseAzureServiceBus(connection: "AzureServiceBus");

        builder.AddCallHandler<MyMessage, MyMessageHandler>();
    }
}
