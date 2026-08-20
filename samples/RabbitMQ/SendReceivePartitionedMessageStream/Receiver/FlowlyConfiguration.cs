using Flowly;
using Flowly.RabbitMQ;
using Messages;
using Receiver.Handlers;

namespace Receiver;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseRabbitMq(connection: "RabbitMQ");

        builder.AddMessageStreamHandler<MyMessage, MyMessageHandler>();
    }
}
