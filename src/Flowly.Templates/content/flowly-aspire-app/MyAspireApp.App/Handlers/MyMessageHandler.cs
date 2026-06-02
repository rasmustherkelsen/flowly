using Flowly;
using MyAspireApp.App.Messages;

namespace MyAspireApp.App.Handlers;

#if (UseCallHandler)
internal class MyMessageHandler : CallHandler<MyMessage, MyReturnMessage>
{
    protected override Task<MyReturnMessage> Handle(IMessageContext<MyMessage> messageContext)
    {
        Console.WriteLine($"Received call: {messageContext.Message.Text}");
        return Task.FromResult(new MyReturnMessage($"Echo: {messageContext.Message.Text}"));
    }
}
#else
internal class MyMessageHandler : MessageHandler<MyMessage>
{
    public override Task Handle(IMessageContext<MyMessage> messageContext)
    {
        Console.WriteLine($"Received: {messageContext.Message.Text}");
        return Task.CompletedTask;
    }
}
#endif
