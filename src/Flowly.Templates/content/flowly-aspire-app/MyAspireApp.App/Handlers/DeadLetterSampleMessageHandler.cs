using Flowly;
using MyAspireApp.App.Messages;

namespace MyAspireApp.App.Handlers;

[RetryPolicy(maxRetries: 2, delaySeconds: 2)]
internal class DeadLetterSampleMessageHandler : MessageHandler<DeadLetterSampleMessage>
{
    public override Task Handle(IMessageContext<DeadLetterSampleMessage> messageContext)
    {
        if (messageContext.Message.Text.StartsWith("[fail]", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Simulated failure.");

        Console.WriteLine($"Received dead-letter sample: {messageContext.Message.Text}");
        return Task.CompletedTask;
    }
}
