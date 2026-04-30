using Flowly;
using Messages;

namespace Receiver.MessageHandlers;

[MaxConcurrentCalls(10)]
public class FlakyMessageHandler : MessageHandler<FlakyMessage>
{
    public override async Task Handle(IMessageContext<FlakyMessage> messageContext)
    {
        Console.WriteLine($"Waiting for {messageContext.Message.DelayInSeconds} seconds...");

        if (Random.Shared.Next(0, 10) < 5)
            throw new InvalidOperationException("Kaboom");

        await Task.Delay(TimeSpan.FromSeconds(messageContext.Message.DelayInSeconds));
    }
}