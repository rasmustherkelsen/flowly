using Flowly;
using Flowly.RabbitMQ;
using Flowly.MessageStreams;
using Messages;
using Receiver.Handlers;

namespace Receiver;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseRabbitMq(connection: "RabbitMQ");

        builder.AddPostgresMessageStreamPersistence("MsgStreamDb");
        builder.AddMessageStreamHandler<MyMessage, MyMessageHandler>();
        builder.UseMessageStreamCheckpoint<MyMessage>();
    }
}
