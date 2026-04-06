using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using MessageContracts;

namespace BackendProcessor.JobHandlers;

class SomeQueryProcessor : MessageHandler<SomeQueryMessage>
{
    public override void Configure(HandlerQueueOptions options)
    {
        options.MaxConcurrentCalls = 5;
        options.MaxRetries = 3;
        options.RetryDelaySeconds = 10;
    }

    public override async Task Handle(IMessageContext<SomeQueryMessage> messageContext)
    {
        Console.WriteLine($"SomeQueryProcessor. Waiting for {messageContext.Message.DelayInSeconds} seconds...");

        if (Random.Shared.Next(0, 10) < 5)
            throw new InvalidOperationException("Kaboom");

        await Task.Delay(TimeSpan.FromSeconds(messageContext.Message.DelayInSeconds));
    }
}
