using Flowly;
using MyAspireApp.App.Messages;

namespace MyAspireApp.App.Handlers;

#if (UseCallHandler)
internal class MyMessageHandler(ILogger<MyMessageHandler> logger) : CallHandler<MyMessage, MyReturnMessage>
{
    protected override Task<MyReturnMessage> Handle(IMessageContext<MyMessage> messageContext)
    {
        logger.LogInformation("Received call: {Text}", messageContext.Message.Text);
        return Task.FromResult(new MyReturnMessage($"Echo: {messageContext.Message.Text}"));
    }
}
#else
#if (UseStream)
[StreamStartPosition(StreamStartPositionKind.First)]
internal class MyMessageHandler(ILogger<MyMessageHandler> logger) : MessageStreamHandler<MyMessage>
{
    public override Task Handle(IMessageStreamContext<MyMessage> messageContext)
    {
        foreach (var message in messageContext.Messages)
        {
            if (messageContext.Partition is not null)
                logger.LogInformation("Received: {Text} (partition {Partition})", message.Text, messageContext.Partition);
            else
                logger.LogInformation("Received: {Text}", message.Text);
        }

        return Task.CompletedTask;
    }
}
#else
internal class MyMessageHandler(ILogger<MyMessageHandler> logger) : MessageHandler<MyMessage>
{
    public override Task Handle(IMessageContext<MyMessage> messageContext)
    {
        logger.LogInformation("Received: {Text}", messageContext.Message.Text);
        return Task.CompletedTask;
    }
}
#endif
#endif
