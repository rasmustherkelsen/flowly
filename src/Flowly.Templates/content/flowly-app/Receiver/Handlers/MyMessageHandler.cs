using Flowly;
using Messages;

namespace Receiver.Handlers;

#if (UseCallHandler)
internal class MyMessageHandler : CallHandler<MyMessage, MyReturnMessage>
{
    protected override Task<MyReturnMessage> Handle(IMessageContext<MyMessage> messageContext)
    {
        Console.WriteLine($"Received call: {messageContext.Message.Text}");
        return Task.FromResult(new MyReturnMessage($"Echo from Receiver: {messageContext.Message.Text}"));
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
