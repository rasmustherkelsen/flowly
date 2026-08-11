using Flowly;
using Flowly.RabbitMQ;
using Messages;

namespace Sender;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseRabbitMq(connection: "RabbitMQ");
        builder.AddMessageRecorder<MyStreamMessage>();
    }
}
