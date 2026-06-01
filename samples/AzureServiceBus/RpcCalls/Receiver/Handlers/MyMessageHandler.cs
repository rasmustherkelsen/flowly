using Flowly;
using Messages;

namespace Receiver.Handlers;

internal class MyMessageHandler : CallHandler<MyMessage, MyReturnMessage>
{
    protected override Task<MyReturnMessage> Handle(IMessageContext<MyMessage> messageContext)
    {
        Console.WriteLine($"Received call: {messageContext.Message.Text}");
        return Task.FromResult(new MyReturnMessage($"Echo from Receiver: {messageContext.Message.Text}"));
    }
}
