using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using MessageContracts;

namespace BackendProcessor.JobHandlers;

public class SomeQueryProcessor : MessageHandlerBase<SomeQueryMessage>
{
    public override async Task Handle(IMessageContext<SomeQueryMessage> messageContext)
    {
        Console.WriteLine($"SomeQueryProcessor. Waiting for {messageContext.Message.DelayInSeconds} seconds...");

        if (Random.Shared.Next(0, 10) < 5)
            throw new InvalidOperationException("Kaboom");
        
        await Task.Delay(TimeSpan.FromSeconds(messageContext.Message.DelayInSeconds));
    }
}