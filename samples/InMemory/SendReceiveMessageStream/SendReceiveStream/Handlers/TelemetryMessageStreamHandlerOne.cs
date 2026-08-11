using Flowly;
using SendReceiveStream.Messages;

namespace SendReceiveStream.Handlers;

[BatchProcessing(100, 10)]
[StreamStartPosition(StreamStartPositionKind.First)]
internal class TelemetryMessageStreamHandlerOne(ILogger<TelemetryMessageStreamHandlerOne> logger) : MessageStreamHandler<TelemetryMessage>
{
    public override Task Handle(IMessageStreamContext<TelemetryMessage> messageContext)
    {
        foreach (var message in messageContext.Messages)
        {
            logger.LogInformation($"Read telemetry message number {message.MessageNumber}");
        }

        return Task.CompletedTask;
    }
}