using Flowly;
using Messages;

namespace Receiver.MessageHandlers;

internal class MyStreamMessageHandler(ILogger<MyStreamMessageHandler> logger) : MessageStreamHandler<MyStreamMessage>
{
    public override void Configure(MessageStreamHandlerOptions options)
    {
        options.StartPosition = StartPosition.First();
    }

    public override async Task Handle(IMessageStreamContext<MyStreamMessage> messageContext)
    {
        foreach (var message in messageContext.Messages)
        {
            logger.LogInformation("Read entry {MessageEntryNumber}", message.EntryNumber);
        }
    }
}