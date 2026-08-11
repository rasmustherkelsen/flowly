using Flowly;
using SendReceiveStream.Messages;

namespace SendReceiveStream.Handlers;

internal class TelemetryMessageStreamHandlerTwo(ILogger<TelemetryMessageStreamHandlerTwo> logger) : MessageStreamHandler<TelemetryMessage>
{
    public override void Configure(MessageStreamHandlerOptions options)
    {
        options.StartPosition = StartPosition.First();
        options.MaxWaitTime = TimeSpan.FromSeconds(5);
        options.MaxMessagesBeforeProcessing = 20;
    }

    public override Task Handle(IMessageStreamContext<TelemetryMessage> messageContext)
    {
        foreach (var message in messageContext.Messages)
        {
            logger.LogInformation($"Read telemetry message number {message.MessageNumber}");
        }

        return Task.CompletedTask;
    }
}