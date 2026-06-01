using Flowly;
using Flowly.InMemory;
using App.Handlers;
using App.Messages;

namespace App;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseInMemory();

        builder.AddCallSubmitter<MyMessage>();
        builder.AddCallHandler<MyMessage, MyMessageHandler>();
    }
}
