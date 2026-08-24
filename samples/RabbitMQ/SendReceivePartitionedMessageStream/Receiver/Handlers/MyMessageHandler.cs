using Flowly;
using Messages;

namespace Receiver.Handlers;

[StreamStartPosition(StreamStartPositionKind.First)]
[BatchProcessing(10, 5)]
internal class MyMessageHandler : MessageStreamHandler<MyMessage>
{
    public override Task Handle(IMessageStreamContext<MyMessage> messageContext)
    {
        foreach (var message in messageContext.Messages)
        {
            Console.WriteLine($"Received: {message.Text}" + (messageContext.Partition is not null ? $" (partition {messageContext.Partition})" : string.Empty));
        }

        return Task.CompletedTask;
    }
}