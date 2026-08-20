using Flowly;
using MyAspireApp.Messages;

namespace MyAspireApp.Receiver.Handlers;

[RetryPolicy(maxRetries: 2, delaySeconds: 2)]
internal class DeadLetterSampleMessageHandler(ILogger<DeadLetterSampleMessageHandler> logger) : MessageHandler<DeadLetterSampleMessage>
{
    public override Task Handle(IMessageContext<DeadLetterSampleMessage> messageContext)
    {
        if (messageContext.Message.Text.StartsWith("[fail]", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Simulated failure.");

        logger.LogInformation("Received dead-letter sample: {Text}", messageContext.Message.Text);
        return Task.CompletedTask;
    }
}
