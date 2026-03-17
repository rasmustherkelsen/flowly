using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using MessageContracts;

namespace BackendProcessor.JobHandlers;

public class SomeQueryProcessor : MessageHandlerBase<SomeQueryMessage>
{
    public override async Task Handle(IMessageContext<SomeQueryMessage> messageContext)
    {
        Console.WriteLine($"SomeQueryProcessor. Waiting for {messageContext.Message.DelayInSeconds} seconds...");
        await Task.Delay(TimeSpan.FromSeconds(messageContext.Message.DelayInSeconds));
    }
}