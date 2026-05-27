using Flowly;
using App.Messages;

namespace App.Handlers;

internal class MyMessageHandler : MessageHandler<MyMessage>
{
    public override Task Handle(IMessageContext<MyMessage> messageContext)
    {
        Console.WriteLine($"Received: {messageContext.Message.Text}");
        return Task.CompletedTask;
    }
}
