using Flowly;
using Messages;

namespace Receiver.Handlers;

[StreamStartPosition(StreamStartPositionKind.First)]
internal class MyMessageHandler(ILogger<MyMessageHandler> logger) : MessageStreamHandler<MyMessage>
{
    public override Task Handle(IMessageStreamContext<MyMessage> messageContext)
    {
        foreach (var message in messageContext.Messages)
        {
            if (messageContext.Partition is not null)
                logger.LogInformation("Received: {Index} - {Text} (partition {Partition})", message.MessageNumber, message.Text, messageContext.Partition);
            else
                logger.LogInformation("Received: {Index} - {Text}", message.MessageNumber, message.Text);
        }

        return Task.CompletedTask;
    }
}
